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
            logger.LogWarning("Tentative de connexion à un salon inexistant: {Channel}", channel);
            await Clients.Caller.SendAsync("ChannelNotFound", channel);
            return;
        }

        var user = await db.ConnectedUsers
            .FirstOrDefaultAsync(u => u.ConnectionId == Context.ConnectionId);

        if (user == null)
        {
            logger.LogWarning("Tentative de connexion à un salon sans utilisateur enregistré");
            await Clients.Caller.SendAsync("Error", "Utilisateur non identifié");
            return;
        }

        var userInChannel = await db.ConnectedUsers
            .FirstOrDefaultAsync(u => u.Username == user.Username && u.Channel == channel);

        if (userInChannel != null)
        {
            logger.LogWarning("Utilisateur {Username} déjà connecté à {Channel}", user.Username, channel);
            return;
        }

        userInChannel = new ConnectedUser
        {
            Id = Guid.NewGuid(),
            Username = user.Username,
            UserId = user.UserId,
            ConnectionId = Context.ConnectionId,
            Channel = channel,
            LastActivity = DateTime.UtcNow,
            ServerInstanceId = user.ServerInstanceId,
            ConnectedAt = DateTime.UtcNow
        };

        user.LastActivity = DateTime.UtcNow;
        db.ConnectedUsers.Add(userInChannel);
        await db.SaveChangesAsync();

        await Groups.AddToGroupAsync(Context.ConnectionId, channel);
        await Clients.Group(channel).SendAsync("UserJoined", user.Username, user.UserId, channel);

        logger.LogInformation("Utilisateur {Username} a rejoint {Channel}", user.Username, channel);
    }

    public async Task LeaveChannel(string channel)
    {
        var userInChannel = await db.ConnectedUsers
            .FirstOrDefaultAsync(u => u.ConnectionId == Context.ConnectionId && u.Channel == channel);

        if (userInChannel == null)
        {
            logger.LogWarning("Utilisateur non trouvé dans {Channel}", channel);
            return;
        }

        db.ConnectedUsers.Remove(userInChannel);
        userInChannel.LastActivity = DateTime.UtcNow;
        await db.SaveChangesAsync();

        await Groups.RemoveFromGroupAsync(Context.ConnectionId, channel);
        await Clients.Group(channel).SendAsync("UserLeft", userInChannel.Username, userInChannel.UserId, channel);

        logger.LogInformation("Utilisateur {Username} a quitté {Channel}", userInChannel.Username, channel);
    }

    [SuppressMessage("Performance", "CA1862", Justification = "Not needed in SQL")]
    public async Task SendMessage(SendMessageRequest request)
    {
        var connectedUser = await db.ConnectedUsers
            .FirstOrDefaultAsync(u => u.ConnectionId == Context.ConnectionId && u.Channel == request.Channel);

        if (connectedUser == null)
        {
            logger.LogWarning("Tentative d'envoi de message sans utilisateur identifié dans {Channel}", request.Channel);
            await Clients.Caller.SendAsync("Error", "Utilisateur non identifié");
            return;
        }

        connectedUser.LastActivity = DateTime.UtcNow;

        var channel = await db.Channels
            .FirstOrDefaultAsync(c => c.Name.ToLower() == request.Channel.ToLower());

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

        var isMuted = await db.MutedUsers
            .AnyAsync(m => m.ChannelName == null || m.ChannelName.ToLower() == request.Channel.ToLower()
                        && m.UserId == connectedUser.UserId);

        var message = new Message
        {
            Id = Guid.NewGuid(),
            UserId = connectedUser.UserId,
            Username = connectedUser.Username,
            Content = request.Content,
            Channel = request.Channel,
            Timestamp = DateTime.UtcNow,
            IsDeleted = isMuted
        };

        db.Messages.Add(message);
        await db.SaveChangesAsync();

        if (isMuted)
        {
            logger.LogInformation(
                "Message de l'utilisateur mute {UserId} sauvegardé mais non diffusé dans {Channel}",
                connectedUser.UserId, request.Channel);
            await Clients.Caller.SendAsync("ReceiveMessage", message);
            return;
        }

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
            IsDeletedByRecipient = isGlobalyMute
        };
        sender.LastActivity = DateTime.UtcNow;

        db.PrivateMessages.Add(privateMessage);
        await db.SaveChangesAsync();

        logger.LogInformation(
            "Message privé envoyé de {Sender} (UserId: {SenderUserId}) à {Recipient} (UserId: {RecipientUserId})",
            sender.Username, sender.UserId, request.RecipientUsername, request.RecipientUserId);

        var recipient = await db.ConnectedUsers
            .Where(u => u.UserId == request.RecipientUserId)
            .OrderByDescending(u => u.LastActivity)
            .FirstOrDefaultAsync();

        if (recipient?.ConnectionId != null && !isGlobalyMute)
        {
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
            logger.LogWarning("Tentative de marquer des messages comme lus sans utilisateur enregistré");
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
        currentUser.LastActivity = DateTime.UtcNow;
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
            .OrderByDescending(u => u.LastActivity)
            .FirstOrDefaultAsync(u => u.ConnectionId == Context.ConnectionId);

        if (user == null)
        {
            user = new ConnectedUser
            {
                Id = Guid.NewGuid(),
                Username = username,
                UserId = userId,
                Channel = null,
                ConnectionId = Context.ConnectionId,
                LastActivity = DateTime.UtcNow,
                ServerInstanceId = _instanceId,
                ConnectedAt = DateTime.UtcNow
            };

            await Clients.All.SendAsync(_userStatusChangedMethod, username, userId, true);
            db.ConnectedUsers.Add(user);
            logger.LogInformation("Utilisateur {Username} enregistré via Ping avec UserId {UserId}", username, userId);
        }
        else
        {
            user.LastActivity = DateTime.UtcNow;
            user.Username = username;
        }

        await db.SaveChangesAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var connectionId = Context.ConnectionId;
        var usersInChannels = await db.ConnectedUsers
            .Where(u => u.ConnectionId == connectionId)
            .ToListAsync();

        if (usersInChannels.Count != 0)
        {
            var username = usersInChannels[0].Username;
            var userId = usersInChannels[0].UserId;
            foreach (var userInChannel in from userInChannel in usersInChannels
                                          where !string.IsNullOrEmpty(userInChannel.Channel)
                                          select userInChannel.Channel)
            {
                await Groups.RemoveFromGroupAsync(connectionId, userInChannel);
                await Clients.Group(userInChannel)
                                    .SendAsync("UserLeft", username, userId, userInChannel);
            }

            db.ConnectedUsers.RemoveRange(usersInChannels);
            await db.SaveChangesAsync();

            var hasOtherConnections = await db.ConnectedUsers
                .AnyAsync(u => u.Username == username);

            if (!hasOtherConnections)
            {
                await Clients.All.SendAsync(_userStatusChangedMethod, username, userId, false);
                logger.LogInformation("Utilisateur {Username} complètement déconnecté", username);
            }
        }

        await base.OnDisconnectedAsync(exception);
    }
}