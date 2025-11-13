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
    private static readonly string _updateUserListMethod = "UpdateUserList";
    private readonly string _instanceId = options.Value.GetInstanceId();

    public async Task JoinChannel(string username, string channel)
    {
        // Vérifier si le canal existe
        var channelExists = await db.Channels
            .AnyAsync(c => c.Name == channel);

        if (!channelExists)
        {
            await Clients.Caller.SendAsync("ChannelNotFound", channel);
            return;
        }

        // Récupérer ou créer l'utilisateur (pas de Save ici !)
        var user = await db.ConnectedUsers
            .FirstOrDefaultAsync(u => u.Username == username
                && u.ConnectionId == Context.ConnectionId);

        if (user == null)
        {
            user = new ConnectedUser
            {
                Username = username,
                Channel = channel,
                ConnectionId = Context.ConnectionId,
                LastPing = DateTime.UtcNow,
                ServerInstanceId = _instanceId
            };

            db.ConnectedUsers.Add(user);
            logger.LogInformation("Nouvel utilisateur {Username} rejoint {Channel}", username, channel);
        }
        else
        {
            // Si l'utilisateur était dans un autre salon, le quitter
            if (user.Channel != null && user.Channel != channel)
            {
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, user.Channel);
                await Clients.Group(user.Channel).SendAsync("UserLeft", username, user.Channel);

                var oldChannelUsers = await GetChannelUsers(user.Channel);
                await Clients.Group(user.Channel).SendAsync(_updateUserListMethod, oldChannelUsers);

                logger.LogInformation("Utilisateur {Username} a quitté {OldChannel}", username, user.Channel);
            }

            user.Channel = channel;
            user.LastPing = DateTime.UtcNow;
        }

        // ✅ Un seul SaveChangesAsync à la fin
        await db.SaveChangesAsync();

        // Rejoindre le nouveau salon
        await Groups.AddToGroupAsync(Context.ConnectionId, channel);

        // Notifier les autres utilisateurs
        await Clients.Group(channel).SendAsync("UserJoined", username, channel);

        // Envoyer la liste mise à jour des utilisateurs
        var channelUsers = await GetChannelUsers(channel);
        await Clients.Group(channel).SendAsync(_updateUserListMethod, channelUsers);

        // Envoyer le statut mute du canal
        var channelInfo = await db.Channels
            .FirstOrDefaultAsync(c => c.Name == channel);

        if (channelInfo != null)
        {
            await Clients.Group(channel).SendAsync("ChannelMuteStatusChanged",
                channel, channelInfo.IsMuted);
        }

        logger.LogInformation("Utilisateur {Username} a rejoint {Channel}", username, channel);
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

            // ✅ Un seul SaveChangesAsync
            await db.SaveChangesAsync();

            await Clients.Group(channel).SendAsync("UserLeft", user.Username, channel);

            var channelUsers = await GetChannelUsers(channel);
            await Clients.Group(channel).SendAsync(_updateUserListMethod, channelUsers);

            logger.LogInformation("Utilisateur {Username} a quitté {Channel}", user.Username, channel);
        }
    }

    [SuppressMessage("Performance", "CA1862", Justification = "Not needed in SQL")]
    public async Task SendMessage(SendMessageRequest request)
    {
        // Vérifier si le canal est muted
        var channel = await db.Channels
            .FirstOrDefaultAsync(c => c.Name.ToLower() == request.Channel.ToLower());

        if (channel != null && channel.IsMuted)
        {
            // Vérifier si l'utilisateur est créateur ou admin
            var user = await db.ReservedUsernames
                .FirstOrDefaultAsync(u => u.Username.ToLower() == request.Username.ToLower());

            var isCreator = channel.CreatedBy.Equals(request.Username, StringComparison.OrdinalIgnoreCase);
            var isAdmin = user?.IsAdmin ?? false;

            if (!isCreator && !isAdmin)
            {
                await Clients.Caller.SendAsync("MessageBlocked",
                    "Ce salon est actuellement muet. Seul le créateur ou un administrateur peut envoyer des messages.");
                return;
            }
        }

        // Mettre à jour la dernière activité
        var connectedUser = await db.ConnectedUsers
            .FirstOrDefaultAsync(u => u.ConnectionId == Context.ConnectionId);

        if (connectedUser != null)
        {
            connectedUser.LastActivity = DateTime.UtcNow;
            connectedUser.LastPing = DateTime.UtcNow;
        }

        var message = new Message
        {
            Id = Guid.NewGuid(),
            Username = request.Username,
            Content = request.Content,
            Channel = request.Channel,
            Timestamp = DateTime.UtcNow,
            IsDeleted = false
        };

        db.Messages.Add(message);

        // ✅ Un seul SaveChangesAsync pour message + LastActivity
        await db.SaveChangesAsync();

        await Clients.Group(request.Channel).SendAsync("ReceiveMessage", message);
    }

    public async Task SendPrivateMessage(SendPrivateMessageRequest request)
    {
        var privateMessage = new PrivateMessage
        {
            Id = Guid.NewGuid(),
            SenderUsername = request.SenderUsername,
            RecipientUsername = request.RecipientUsername,
            Content = request.Content,
            Timestamp = DateTime.UtcNow,
            IsRead = false,
            IsDeleted = false
        };

        db.PrivateMessages.Add(privateMessage);

        // ✅ Un seul SaveChangesAsync
        await db.SaveChangesAsync();

        // Trouver la connexion du destinataire
        var recipientConnection = await db.ConnectedUsers
            .Where(u => u.Username == request.RecipientUsername)
            .Select(u => u.ConnectionId)
            .FirstOrDefaultAsync();

        // Envoyer au destinataire s'il est connecté
        if (recipientConnection != null)
        {
            await Clients.Client(recipientConnection).SendAsync("ReceivePrivateMessage", privateMessage);
        }

        // Envoyer confirmation à l'expéditeur
        await Clients.Caller.SendAsync("PrivateMessageSent", privateMessage);
    }

    public async Task MarkPrivateMessagesAsRead(string senderUsername)
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
            .Where(m => m.RecipientUsername == currentUser.Username
                     && m.SenderUsername == senderUsername
                     && !m.IsRead)
            .ToListAsync();

        foreach (var message in unreadMessages)
        {
            message.IsRead = true;
        }

        // ✅ Un seul SaveChangesAsync pour tous les messages
        await db.SaveChangesAsync();

        // Notifier l'expéditeur que ses messages ont été lus
        var senderConnection = await db.ConnectedUsers
            .Where(u => u.Username == senderUsername)
            .Select(u => u.ConnectionId)
            .FirstOrDefaultAsync();

        if (senderConnection != null)
        {
            await Clients.Client(senderConnection)
                .SendAsync("PrivateMessagesRead", currentUser.Username, unreadMessages.Select(m => m.Id).ToList());
        }
    }

    public async Task Ping(string username)
    {
        // Récupérer ou créer l'utilisateur
        var user = await db.ConnectedUsers
            .FirstOrDefaultAsync(u => u.Username == username
                && u.ConnectionId == Context.ConnectionId);

        if (user == null)
        {
            user = new ConnectedUser
            {
                Username = username,
                Channel = null,
                ConnectionId = Context.ConnectionId,
                LastPing = DateTime.UtcNow,
                ServerInstanceId = _instanceId
            };

            await Clients.All.SendAsync(_userStatusChangedMethod, username, true);
            db.ConnectedUsers.Add(user);
            logger.LogInformation("Utilisateur {Username} enregistré via Ping", username);
        }
        else
        {
            user.LastPing = DateTime.UtcNow;
        }

        // ✅ Un seul SaveChangesAsync
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
                await Clients.Group(channel).SendAsync("UserLeft", user.Username, channel);

                var users = await db.ConnectedUsers
                    .Where(u => u.Channel == channel)
                    .OrderBy(u => u.Username)
                    .Select(u => new User
                    {
                        Username = u.Username,
                        ConnectedAt = u.ConnectedAt,
                        ConnectionId = u.ConnectionId
                    })
                    .ToListAsync();

                await Clients.Group(channel).SendAsync("UpdateUserList", users);
            }

            var username = user.Username;
            // Vérifier si l'utilisateur n'a plus de connexions actives
            var hasOtherConnections = await db.ConnectedUsers
                .AnyAsync(u => u.Username == username);

            if (!hasOtherConnections && !string.IsNullOrEmpty(username))
            {
                // Notifier tous les clients que l'utilisateur est hors ligne
                await Clients.All.SendAsync(_userStatusChangedMethod, username, false);
            }
        }

        await base.OnDisconnectedAsync(exception);
    }

    private async Task<List<User>> GetChannelUsers(string channel)
    {
        return await db.ConnectedUsers
            .Where(u => u.Channel == channel)
            .OrderBy(u => u.Username)
            .Select(u => new User
            {
                Username = u.Username,
                ConnectedAt = u.ConnectedAt,
                ConnectionId = u.ConnectionId
            })
            .ToListAsync();
    }
}