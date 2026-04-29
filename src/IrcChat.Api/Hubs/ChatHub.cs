using System.Diagnostics.CodeAnalysis;
using IrcChat.Api.Data;
using IrcChat.Api.Extensions;
using IrcChat.Api.Services;
using IrcChat.Shared.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace IrcChat.Api.Hubs;

[SuppressMessage("Performance", "CA1862", Justification = "Not translated in SQL requests")]
public class ChatHub(
    ChatDbContext db,
    IOptions<ConnectionManagerOptions> options,
    ILogger<ChatHub> logger) : Hub
{
    [SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "Constante")]
    private readonly string Error = "Error";
    [SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "Constante")]
    private static readonly string UserStatusChangedMethod = "UserStatusChanged";
    [SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "Constante")]
    private static readonly string ReceiveEphemeralPhoto = "ReceiveEphemeralPhoto";
    private readonly string _instanceId = options.Value.GetInstanceId();

    public async Task JoinChannel(string channel, int userAge)
    {
        var channelEntity = await db.Channels
            .FirstOrDefaultAsync(c => c.Name == channel);

        if (channelEntity == null)
        {
            logger.LogWarning("Tentative de connexion à un salon inexistant: {Channel}", channel);
            await Clients.Caller.SendAsync("ChannelNotFound", channel);
            return;
        }

        if (channelEntity.MinimumAge > 0 && userAge < channelEntity.MinimumAge)
        {
            logger.LogWarning(
                "Accès refusé au salon {Channel}: âge requis {MinimumAge}, âge fourni {UserAge}",
                channel,
                channelEntity.MinimumAge,
                userAge);
            await Clients.Caller.SendAsync(
                Error,
                $"Accès refusé : vous devez avoir au moins {channelEntity.MinimumAge} ans");
            return;
        }

        var user = await db.ConnectedUsers
            .FirstOrDefaultAsync(u => u.ConnectionId == Context.ConnectionId);

        if (user == null)
        {
            logger.LogWarning("Tentative de connexion à un salon sans utilisateur enregistré");
            await Clients.Caller.SendAsync(Error, "Utilisateur non identifié");
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
            ConnectedAt = DateTime.UtcNow,
            IsNoPvMode = user.IsNoPvMode,
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
            await Clients.Caller.SendAsync(Error, "Utilisateur non identifié");
            return;
        }

        connectedUser.LastActivity = DateTime.UtcNow;

        var channel = await db.Channels
            .FirstOrDefaultAsync(c => c.Name.ToLower() == request.Channel.ToLower());

        if (!await CanSendToChannelAsync(connectedUser, channel))
        {
            return;
        }

        var isMuted = await db.MutedUsers
            .AnyAsync(m => m.ChannelName == null || (m.ChannelName.ToLower() == request.Channel.ToLower()
                        && m.UserId == connectedUser.UserId));

        var message = new Message
        {
            Id = Guid.NewGuid(),
            UserId = connectedUser.UserId,
            Username = connectedUser.Username,
            Content = request.Content,
            Channel = request.Channel,
            Timestamp = DateTime.UtcNow,
            IsDeleted = isMuted,
        };

        db.Messages.Add(message);
        await db.SaveChangesAsync();

        if (isMuted)
        {
            logger.LogInformation(
                "Message de l'utilisateur mute {UserId} sauvegardé mais non diffusé dans {Channel}",
                connectedUser.UserId,
                request.Channel);
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
            await Clients.Caller.SendAsync(Error, "Utilisateur non identifié");
            return;
        }

        // Vérifier si le destinataire est en mode no PV
        var recipient = await db.ConnectedUsers
            .Where(u => u.UserId == request.RecipientUserId)
            .OrderByDescending(u => u.LastActivity)
            .FirstOrDefaultAsync();

        var flowControl = await CanSendMessageToRecipientAsync(recipient, sender);
        if (!flowControl)
        {
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
            IsDeletedByRecipient = isGlobalyMute,
        };
        sender.LastActivity = DateTime.UtcNow;

        db.PrivateMessages.Add(privateMessage);
        await db.SaveChangesAsync();

        logger.LogInformation(
            "Message privé envoyé de {Sender} (UserId: {SenderUserId}) à {Recipient} (UserId: {RecipientUserId})",
            sender.Username,
            sender.UserId,
            request.RecipientUsername,
            request.RecipientUserId);

        if (!isGlobalyMute)
        {
            await Clients.Client(recipient!.ConnectionId).SendAsync("ReceivePrivateMessage", privateMessage);
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

    public async Task Ping(string username, string userId, bool isNoPvMode = false)
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
                ConnectedAt = DateTime.UtcNow,
                IsNoPvMode = isNoPvMode,
            };

            await Clients.All.SendAsync(UserStatusChangedMethod, username, userId, true);
            db.ConnectedUsers.Add(user);
            logger.LogInformation(
                "Utilisateur {Username} enregistré via Ping avec UserId {UserId}, IsNoPvMode={IsNoPvMode}",
                username,
                userId,
                isNoPvMode);
        }
        else
        {
            user.LastActivity = DateTime.UtcNow;
            user.Username = username;
            user.IsNoPvMode = isNoPvMode;
        }

        await db.SaveChangesAsync();
    }


    /// <summary>
    /// Envoie une photo éphémère (3 secondes d'affichage) avec URL Cloudinary.
    /// </summary>
    /// <param name="channelOrUserId">ID du canal ou userId du destinataire.</param>
    /// <param name="imageUrl">URL Cloudinary de l'image full-size.</param>
    /// <param name="thumbnailUrl">URL Cloudinary de la thumbnail floutée.</param>
    /// <param name="isPrivate">True si message privé, False si canal public.</param>
    public async Task SendEphemeralPhoto(string channelOrUserId, string imageUrl, string thumbnailUrl, bool isPrivate)
    {
        var currentUser = await db.ConnectedUsers
                   .FirstOrDefaultAsync(u => u.ConnectionId == Context.ConnectionId);
        if (currentUser == null)
        {
            logger.LogWarning("Tentative d'envoi de photo éphémère sans utilisateur enregistré");
            return;
        }

        currentUser.LastActivity = DateTime.UtcNow;
        await db.SaveChangesAsync();

        var userId = currentUser.UserId;
        var userName = currentUser.Username;

        logger.LogInformation("Envoi photo éphémère de {Username} pour {Target} (privé: {IsPrivate})",
                currentUser.Username, channelOrUserId, isPrivate);

        // Créer le DTO
        var ephemeralPhoto = new EphemeralPhotoDto
        {
            Id = Guid.NewGuid(),
            SenderId = userId,
            SenderUsername = userName,
            ChannelId = isPrivate ? null : channelOrUserId,
            RecipientId = isPrivate ? channelOrUserId : null,
            ImageUrl = imageUrl,
            ThumbnailUrl = thumbnailUrl,
            Timestamp = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddSeconds(3)
        };

        // Broadcast selon le contexte
        if (isPrivate)
        {
            await SendPrivateEphemeralPhoto(channelOrUserId, currentUser, ephemeralPhoto);
            return;
        }

        var channel = await db.Channels
            .FirstOrDefaultAsync(c => c.Name.ToLower() == channelOrUserId.ToLower());

        if (!await CanSendToChannelAsync(currentUser, channel))
        {
            return;
        }

        var isMuted = await db.MutedUsers
            .AnyAsync(m => m.ChannelName == null || (m.ChannelName.ToLower() == channelOrUserId.ToLower()
                        && m.UserId == currentUser.UserId));

        if (isMuted)
        {
            logger.LogInformation(
                "Photo de l'utilisateur muté {UserId} non diffusée dans {Channel}",
                currentUser.UserId,
                channelOrUserId);
            await Clients.Caller.SendAsync(ReceiveEphemeralPhoto, ephemeralPhoto);
            return;
        }

        // Canal public : broadcast à tous les utilisateurs du canal
        await Clients.Group(channelOrUserId).SendAsync(ReceiveEphemeralPhoto, ephemeralPhoto);
        logger.LogInformation("Photo éphémère diffusée dans le canal {Channel}", channelOrUserId);
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
                await Clients.All.SendAsync(UserStatusChangedMethod, username, userId, false);
                logger.LogInformation("Utilisateur {Username} complètement déconnecté", username);
            }
        }

        await base.OnDisconnectedAsync(exception);
    }

    private async Task SendPrivateEphemeralPhoto(string channelOrUserId, ConnectedUser currentUser, EphemeralPhotoDto ephemeralPhoto)
    {
        // Message privé : envoyer au destinataire uniquement
        var recipient = await db.ConnectedUsers
            .Where(u => u.UserId == channelOrUserId)
            .OrderByDescending(u => u.LastActivity)
            .FirstOrDefaultAsync();

        if (!await CanSendMessageToRecipientAsync(recipient, currentUser))
        {
            return;
        }

        var isGlobalyMute = await db.MutedUsers
            .Where(m => m.ChannelName == null
                && (m.UserId == currentUser.UserId || m.UserId == recipient!.UserId))
            .AnyAsync();


        if (!isGlobalyMute)
        {
            await Clients.Client(recipient!.ConnectionId).SendAsync(ReceiveEphemeralPhoto, ephemeralPhoto);
            logger.LogInformation("Photo éphémère envoyée en privé à {Recipient}", channelOrUserId);
        }

        // Envoyer aussi à l'expéditeur (confirmation)
        await Clients.Caller.SendAsync(ReceiveEphemeralPhoto, ephemeralPhoto);
    }

    private async Task<bool> CanSendMessageToRecipientAsync(ConnectedUser? recipient, ConnectedUser sender)
    {
        if (recipient?.ConnectionId == null)
        {
            logger.LogWarning("Tentative d'envoi de message privé sans recipient identifié");
            return false;
        }

        if (!recipient.IsNoPvMode)
        {
            return true;
        }

        // Si destinataire en mode no PV, vérifier s'il existe une conversation
        var hasConversation = await db.PrivateMessages
            .AnyAsync(m =>
                ((m.SenderUserId == recipient.UserId && m.RecipientUserId == sender.UserId) ||
                    (m.SenderUserId == sender.UserId && m.RecipientUserId == recipient.UserId))
                && !(m.SenderUserId == recipient.UserId && m.IsDeletedBySender)
                && !(m.RecipientUserId == recipient.UserId && m.IsDeletedByRecipient));

        if (!hasConversation)
        {
            logger.LogInformation(
                "Message privé bloqué: {Sender} -> {Recipient} (destinataire en mode no PV)",
                sender.Username,
                recipient.Username);

            await Clients.Caller.SendAsync(
                "MessageBlocked",
                "Cet utilisateur ne reçoit pas de messages privés non sollicités.");
            return false;
        }

        return true;
    }

    private async Task<bool> CanSendToChannelAsync(ConnectedUser connectedUser, Channel? channel)
    {
        if (channel != null && channel.IsMuted)
        {
            var user = await db.ReservedUsernames
                .FirstOrDefaultAsync(u => u.Username.ToLower() == connectedUser.Username.ToLower());

            var isCreator = channel.CreatedBy.Equals(connectedUser.Username, StringComparison.OrdinalIgnoreCase);
            var isAdmin = user?.IsAdmin ?? false;

            if (!isCreator && !isAdmin)
            {
                await Clients.Caller.SendAsync(
                    "MessageBlocked",
                    "Ce salon est actuellement muet. Seul le créateur ou un administrateur peut envoyer des messages.");
                return false;
            }
        }

        return true;
    }
}