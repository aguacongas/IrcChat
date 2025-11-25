using System.Diagnostics.CodeAnalysis;
using IrcChat.Api.Data;
using IrcChat.Api.Extensions;
using IrcChat.Api.Services;
using IrcChat.Shared.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace IrcChat.Api.Hubs;

public class ChatHub(
    ChatDbContext db,
    IOptions<ConnectionManagerOptions> options,
    ILogger<ChatHub> logger) : Hub
{
    private static readonly string _userStatusChangedMethod = "UserStatusChanged";
    private readonly string _instanceId = options.Value.GetInstanceId();

    public async Task JoinChannel(string channel)
    {
        var channelExists = await db.Channels
            .AnyAsync(c => c.Name == channel);

        if (!channelExists)
        {
            await Clients.Caller.SendAsync("ChannelNotFound", channel);
            return;
        }

        var user = await db.ConnectedUsers
            .FirstOrDefaultAsync(u => u.ConnectionId == Context.ConnectionId);

        if (user == null)
        {
            logger.LogWarning("Tentative de connexion sur un salon sans expéditeur identifié");
            await Clients.Caller.SendAsync("Error", "Utilisateur non identifié");
            return;
        }

        if (user.Channel != null && user.Channel != channel)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, user.Channel);
            await Clients.Group(user.Channel).SendAsync("UserLeft", user.Username, user.UserId, user.Channel);

            logger.LogInformation("Utilisateur {Username} a quitté {OldChannel}", user.Username, user.Channel);
        }

        user.Channel = channel;
        user.LastPing = DateTime.UtcNow;

        await db.SaveChangesAsync();

        await Groups.AddToGroupAsync(Context.ConnectionId, channel);
        await Clients.Group(channel).SendAsync("UserJoined", user.Username, user.UserId, channel);

        logger.LogInformation("Utilisateur {Username} a rejoint {Channel}", user.Username, channel);
    }

    public async Task LeaveChannel(string channel)
    {
        var user = await db.ConnectedUsers
            .FirstOrDefaultAsync(u => u.ConnectionId == Context.ConnectionId);

        if (user != null && user.Channel == channel)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, channel);

            user.Channel = null;
            user.LastPing = DateTime.UtcNow;

            await db.SaveChangesAsync();

            await Clients.Group(channel).SendAsync("UserLeft", user.Username, user.UserId, channel);

            logger.LogInformation("Utilisateur {Username} a quitté {Channel}", user.Username, channel);
        }
    }

    [SuppressMessage("Performance", "CA1862", Justification = "Not needed in SQL")]
    public async Task SendMessage(SendMessageRequest request)
    {
        var connectedUser = await db.ConnectedUsers
            .FirstOrDefaultAsync(u => u.ConnectionId == Context.ConnectionId);

        if (connectedUser == null)
        {
            logger.LogWarning("Tentative d'envoi de message sans expéditeur identifié");
            await Clients.Caller.SendAsync("Error", "Utilisateur non identifié");
            return;
        }

        connectedUser.LastActivity = DateTime.UtcNow;
        connectedUser.LastPing = DateTime.UtcNow;

        var channel = await db.Channels
            .FirstOrDefaultAsync(c => c.Name.ToLower() == request.Channel.ToLower());

        // Vérifier si le salon est en mode mute général
        if (channel != null && channel.IsMuted)
        {
            var user = await db.ReservedUsernames
                .FirstOrDefaultAsync(u => u.Username.ToLower() == connectedUser.Username.ToLower());

            var isCreator = channel.CreatedBy.Equals(connectedUser.Username, StringComparison.OrdinalIgnoreCase);
            var isAdmin = user?.IsAdmin ?? false;

            if (!isCreator && !isAdmin)
            {
                await Clients.Caller.SendAsync("MessageBlocked",
                    "Ce salon est actuellement muet. Seul le créateur ou un administrateur peut envoyer des messages.");
                return;
            }
        }

        // Vérifier si l'utilisateur est mute dans ce salon
        var isMuted = await db.MutedUsers
            .AnyAsync(m => m.ChannelName == null || m.ChannelName.ToLower() == request.Channel.ToLower()
                        && m.UserId == connectedUser.UserId);

        // Créer le message (sauvegardé dans tous les cas pour audit)
        var message = new Message
        {
            Id = Guid.NewGuid(),
            UserId = connectedUser.UserId,
            Username = connectedUser.Username,
            Content = request.Content,
            Channel = request.Channel,
            Timestamp = DateTime.UtcNow,
            IsDeleted = isMuted // Marquer comme supprimé si l'utilisateur est mute pour eviter qu'il soit retourné par les historiques
        };

        db.Messages.Add(message);
        await db.SaveChangesAsync();

        if (isMuted)
        {
            // Seul l'utilisateur mute reçoit la notification
            // Son message est sauvegardé en BDD mais pas diffusé
            logger.LogInformation(
                "Message de l'utilisateur mute {UserId} sauvegardé mais non diffusé dans {Channel}",
                connectedUser.UserId, request.Channel);
            await Clients.Caller.SendAsync("ReceiveMessage", message);
            return;
        }

        // Diffuser le message à tout le groupe
        await Clients.Group(request.Channel).SendAsync("ReceiveMessage", message);
    }

    public async Task SendPrivateMessage(SendPrivateMessageRequest request)
    {
        var sender = await db.ConnectedUsers
            .FirstOrDefaultAsync(u => u.ConnectionId == Context.ConnectionId);

        if (sender == null)
        {
            logger.LogWarning("Tentative d'envoi de message privé sans expéditeur identifié");
            await Clients.Caller.SendAsync("Error", "Utilisateur non identifié");
            return;
        }

        var isGlobalyMute = await db.MutedUsers
            .Where(m => m.ChannelName == null
                && (m.UserId == sender.UserId || m.UserId == request.RecipientUserId))
            .AnyAsync();

        var privateMessage = new PrivateMessage
        {
            Id = Guid.NewGuid(),
            SenderUsername = sender.Username,
            SenderUserId = sender.UserId,
            RecipientUsername = request.RecipientUsername,
            RecipientUserId = request.RecipientUserId,
            Content = request.Content,
            Timestamp = DateTime.UtcNow,
            IsDeletedByRecipient = isGlobalyMute // Si l'expediteur ou le destinataure est muté globalement, on considère que le destinataire a "supprimé" le message pour qu'il ne le voie pas
        };

        db.PrivateMessages.Add(privateMessage);
        await db.SaveChangesAsync();

        logger.LogInformation(
            "Message privé envoyé de {Sender} (UserId: {SenderUserId}) à {Recipient} (UserId: {RecipientUserId})",
            sender.Username, sender.UserId, request.RecipientUsername, request.RecipientUserId);

        var recipient = await db.ConnectedUsers
            .Where(u => u.UserId == request.RecipientUserId)
            .OrderByDescending(u => u.LastPing)
            .FirstOrDefaultAsync();

        if (recipient?.ConnectionId != null && !isGlobalyMute)
        {
            // Si ni le destinataire ni l'expéditeur ne sont globalement mutés, on envoie le message au recipient
            await Clients.Client(recipient.ConnectionId).SendAsync("ReceivePrivateMessage", privateMessage);
        }

        await Clients.Caller.SendAsync("PrivateMessageSent", privateMessage);
    }

    public async Task MarkPrivateMessagesAsRead(string senderUserId)
    {
        var currentUser = await db.ConnectedUsers
            .FirstOrDefaultAsync(u => u.ConnectionId == Context.ConnectionId);

        if (currentUser == null)
        {
            logger.LogWarning("Tentative de marquer des messages comme lus sans utilisateur enregistré (ConnectionId: {ConnectionId})",
                Context.ConnectionId);
            return;
        }

        var unreadMessages = await db.PrivateMessages
            .Where(m => m.RecipientUserId == currentUser.UserId
                     && m.SenderUserId == senderUserId
                     && !m.IsRead)
            .ToListAsync();

        foreach (var message in unreadMessages)
        {
            message.IsRead = true;
        }

        await db.SaveChangesAsync();

        var senderConnection = await db.ConnectedUsers
            .Where(u => u.UserId == senderUserId)
            .Select(u => u.ConnectionId)
            .FirstOrDefaultAsync();

        if (senderConnection != null)
        {
            await Clients.Client(senderConnection)
                .SendAsync("PrivateMessagesRead", currentUser.Username, unreadMessages.Select(m => m.Id).ToList());
        }
    }

    public async Task Ping(string username, string userId)
    {
        var user = await db.ConnectedUsers
            .FirstOrDefaultAsync(u => u.ConnectionId == Context.ConnectionId);

        if (user == null)
        {
            user = new ConnectedUser
            {
                Username = username,
                UserId = userId,
                Channel = null,
                ConnectionId = Context.ConnectionId,
                LastPing = DateTime.UtcNow,
                ServerInstanceId = _instanceId
            };

            await Clients.All.SendAsync(_userStatusChangedMethod, username, userId, true);
            db.ConnectedUsers.Add(user);
            logger.LogInformation("Utilisateur {Username} enregistré via Ping avec UserId {UserId}", username, userId);
        }
        else
        {
            user.LastPing = DateTime.UtcNow;
            user.Username = username;
        }

        await db.SaveChangesAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var connectionId = Context.ConnectionId;
        var user = await db.ConnectedUsers
            .FirstOrDefaultAsync(u => u.ConnectionId == connectionId);

        if (user != null)
        {
            var channel = user.Channel;
            db.ConnectedUsers.Remove(user);
            await db.SaveChangesAsync();

            if (!string.IsNullOrEmpty(channel))
            {
                await Groups.RemoveFromGroupAsync(connectionId, channel);
                await Clients.Group(channel).SendAsync("UserLeft", user.Username, user.UserId, channel);
            }

            var username = user.Username;
            var hasOtherConnections = await db.ConnectedUsers
                .AnyAsync(u => u.Username == username);

            if (!hasOtherConnections && !string.IsNullOrEmpty(username))
            {
                await Clients.All.SendAsync(_userStatusChangedMethod, username, user.UserId, false);
            }
        }

        await base.OnDisconnectedAsync(exception);
    }
}