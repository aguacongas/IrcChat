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
    ILogger<ChatHub> logger,
    IHttpContextAccessor httpContextAccessor) : Hub
{
    private CancellationToken RequestToken => httpContextAccessor.HttpContext?.RequestAborted ?? CancellationToken.None;

    [SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "Constante")]
    private static readonly string UserNotIdentified = "Utilisateur non identifié";
    [SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "Constante")]
    private static readonly string Error = "Error";
    [SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "Constante")]
    private static readonly string UserStatusChangedMethod = "UserStatusChanged";
    [SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "Constante")]
    private static readonly string ReceiveEphemeralPhoto = "ReceiveEphemeralPhoto";
    private readonly string _instanceId = options.Value.GetInstanceId();

    public async Task JoinChannel(string channel, int userAge)
    {
        var channelEntity = await db.Channels
            .FirstOrDefaultAsync(c => c.Name == channel, RequestToken);

        if (channelEntity == null)
        {
            logger.LogWarning("Tentative de connexion à un salon inexistant: {Channel}", channel);
            await Clients.Caller.SendAsync("ChannelNotFound", channel, RequestToken);
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
                $"Accès refusé : vous devez avoir au moins {channelEntity.MinimumAge} ans",
                RequestToken);
            return;
        }

        var user = await db.ConnectedUsers
            .FirstOrDefaultAsync(u => u.ConnectionId == Context.ConnectionId, RequestToken);

        if (user == null)
        {
            logger.LogWarning("Tentative de connexion à un salon sans utilisateur enregistré");
            await Clients.Caller.SendAsync(Error, UserNotIdentified, RequestToken);
            return;
        }

        var userInChannel = await db.ConnectedUsers
            .FirstOrDefaultAsync(u => u.Username == user.Username && u.Channel == channel, RequestToken);

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
        await db.SaveChangesAsync(RequestToken);

        await Groups.AddToGroupAsync(Context.ConnectionId, channel, RequestToken);
        await Clients.Group(channel).SendAsync("UserJoined", user.Username, user.UserId, channel, RequestToken);

        logger.LogInformation("Utilisateur {Username} a rejoint {Channel}", user.Username, channel);
    }

    public async Task LeaveChannel(string channel)
    {
        var userInChannel = await db.ConnectedUsers
            .FirstOrDefaultAsync(u => u.ConnectionId == Context.ConnectionId && u.Channel == channel, RequestToken);

        if (userInChannel == null)
        {
            logger.LogWarning("Utilisateur non trouvé dans {Channel}", channel);
            return;
        }

        db.ConnectedUsers.Remove(userInChannel);
        userInChannel.LastActivity = DateTime.UtcNow;
        await db.SaveChangesAsync(RequestToken);

        await Groups.RemoveFromGroupAsync(Context.ConnectionId, channel, RequestToken);
        await Clients.Group(channel).SendAsync("UserLeft", userInChannel.Username, userInChannel.UserId, channel, RequestToken);

        logger.LogInformation("Utilisateur {Username} a quitté {Channel}", userInChannel.Username, channel);
    }

    [SuppressMessage("Performance", "CA1862", Justification = "Not needed in SQL")]
    public async Task SendMessage(SendMessageRequest request)
    {
        var connectedUser = await db.ConnectedUsers
            .FirstOrDefaultAsync(u => u.ConnectionId == Context.ConnectionId && u.Channel == request.Channel, RequestToken);

        if (connectedUser == null)
        {
            logger.LogWarning("Tentative d'envoi de message sans utilisateur identifié dans {Channel}", request.Channel);
            await Clients.Caller.SendAsync(Error, UserNotIdentified, RequestToken);
            return;
        }

        connectedUser.LastActivity = DateTime.UtcNow;

        var channel = await db.Channels
            .FirstOrDefaultAsync(c => c.Name.ToLower() == request.Channel.ToLower(), RequestToken);

        if (!await CanSendToChannelAsync(connectedUser, channel, RequestToken))
        {
            return;
        }

        var isMuted = await db.MutedUsers
            .AnyAsync(m => m.ChannelName == null || (m.ChannelName.ToLower() == request.Channel.ToLower()
                        && m.UserId == connectedUser.UserId), RequestToken);

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
        await db.SaveChangesAsync(RequestToken);

        if (isMuted)
        {
            logger.LogInformation(
                "Message de l'utilisateur mute {UserId} sauvegardé mais non diffusé dans {Channel}",
                connectedUser.UserId,
                request.Channel);
            await Clients.Caller.SendAsync("ReceiveMessage", message, RequestToken);
            return;
        }

        await Clients.Group(request.Channel).SendAsync("ReceiveMessage", message, RequestToken);
    }

    public async Task SendPrivateMessage(SendPrivateMessageRequest request)
    {
        var sender = await db.ConnectedUsers
            .FirstOrDefaultAsync(u => u.ConnectionId == Context.ConnectionId, RequestToken);

        if (sender == null)
        {
            logger.LogWarning("Tentative d'envoi de message privé sans expéditeur identifié");
            await Clients.Caller.SendAsync(Error, UserNotIdentified, RequestToken);
            return;
        }

        var recipient = await db.ConnectedUsers
            .Where(u => u.UserId == request.RecipientUserId)
            .OrderByDescending(u => u.LastActivity)
            .FirstOrDefaultAsync(RequestToken);

        var flowControl = await CanSendMessageToRecipientAsync(recipient, sender, RequestToken);
        if (!flowControl)
        {
            return;
        }

        var isGlobalyMute = await db.MutedUsers
            .Where(m => m.ChannelName == null
                && (m.UserId == sender.UserId || m.UserId == request.RecipientUserId))
            .AnyAsync(RequestToken);

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
        await db.SaveChangesAsync(RequestToken);

        logger.LogInformation(
            "Message privé envoyé de {Sender} (UserId: {SenderUserId}) à {Recipient} (UserId: {RecipientUserId})",
            sender.Username,
            sender.UserId,
            request.RecipientUsername,
            request.RecipientUserId);

        if (!isGlobalyMute)
        {
            await Clients.Client(recipient!.ConnectionId).SendAsync("ReceivePrivateMessage", privateMessage, RequestToken);
        }

        await Clients.Caller.SendAsync("PrivateMessageSent", privateMessage, RequestToken);
    }

    public async Task MarkPrivateMessagesAsRead(string senderUserId)
    {
        var currentUser = await db.ConnectedUsers
            .FirstOrDefaultAsync(u => u.ConnectionId == Context.ConnectionId, RequestToken);

        if (currentUser == null)
        {
            logger.LogWarning("Tentative de marquer des messages comme lus sans utilisateur enregistré");
            return;
        }

        var unreadMessages = await db.PrivateMessages
            .Where(m => m.RecipientUserId == currentUser.UserId
                     && m.SenderUserId == senderUserId
                     && !m.IsRead)
            .ToListAsync(RequestToken);

        foreach (var message in unreadMessages)
        {
            message.IsRead = true;
        }

        currentUser.LastActivity = DateTime.UtcNow;
        await db.SaveChangesAsync(RequestToken);

        var senderConnection = await db.ConnectedUsers
            .Where(u => u.UserId == senderUserId)
            .Select(u => u.ConnectionId)
            .FirstOrDefaultAsync(RequestToken);

        if (senderConnection != null)
        {
            await Clients.Client(senderConnection)
                .SendAsync("PrivateMessagesRead", currentUser.Username, unreadMessages.Select(m => m.Id).ToList(), RequestToken);
        }
    }

    public async Task Ping(string username, string userId, bool isNoPvMode = false)
    {
        var user = await db.ConnectedUsers
            .OrderByDescending(u => u.LastActivity)
            .FirstOrDefaultAsync(u => u.ConnectionId == Context.ConnectionId, RequestToken);

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

            await Clients.All.SendAsync(UserStatusChangedMethod, username, userId, true, RequestToken);
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

        await db.SaveChangesAsync(RequestToken);
    }

    /// <summary>
    /// Réagit à un message par un emoji.
    /// - Si l'utilisateur n'a pas encore réagi : ajoute la réaction.
    /// - Si l'utilisateur a réagi avec le même emoji : retire la réaction (toggle).
    /// - Si l'utilisateur a réagi avec un emoji différent : remplace la réaction.
    /// Broadcast MessageReactionUpdated à tout le groupe du salon.
    /// </summary>
    /// <param name="messageId">Identifiant du message.</param>
    /// <param name="emoji">L'emoji choisi.</param>
    public async Task ReactToMessage(Guid messageId, string emoji)
    {
        var currentUser = await db.ConnectedUsers
            .FirstOrDefaultAsync(u => u.ConnectionId == Context.ConnectionId, RequestToken);

        if (currentUser == null)
        {
            logger.LogWarning("Tentative de réaction sans utilisateur enregistré");
            await Clients.Caller.SendAsync(Error, UserNotIdentified, RequestToken);
            return;
        }

        var message = await db.Messages.FindAsync(messageId, RequestToken);
        if (message == null || message.IsDeleted)
        {
            logger.LogWarning("Tentative de réaction sur un message inexistant ou supprimé: {MessageId}", messageId);
            return;
        }

        // Vérifier l'existence d'une réaction existante de cet utilisateur
        var existingReaction = await db.MessageReactions
            .FirstOrDefaultAsync(r => r.MessageId == messageId && r.UserId == currentUser.UserId, RequestToken);

        if (existingReaction != null)
        {
            if (existingReaction.Emoji == emoji)
            {
                // Même emoji : retrait de la réaction (toggle off)
                db.MessageReactions.Remove(existingReaction);
                logger.LogInformation(
                    "Réaction {Emoji} retirée par {Username} sur le message {MessageId}",
                    emoji,
                    currentUser.Username,
                    messageId);
            }
            else
            {
                // Emoji différent : remplacement
                existingReaction.Emoji = emoji;
                existingReaction.CreatedAt = DateTime.UtcNow;
                logger.LogInformation(
                    "Réaction remplacée par {Emoji} pour {Username} sur le message {MessageId}",
                    emoji,
                    currentUser.Username,
                    messageId);
            }
        }
        else
        {
            // Nouvelle réaction
            var reaction = new MessageReaction
            {
                Id = Guid.NewGuid(),
                MessageId = messageId,
                UserId = currentUser.UserId,
                Username = currentUser.Username,
                Emoji = emoji,
                CreatedAt = DateTime.UtcNow,
            };
            db.MessageReactions.Add(reaction);
            logger.LogInformation(
                "Réaction {Emoji} ajoutée par {Username} sur le message {MessageId}",
                emoji,
                currentUser.Username,
                messageId);
        }

        await db.SaveChangesAsync(RequestToken);

        // Recalculer les réactions agrégées pour ce message
        var updatedReactions = await db.MessageReactions
            .Where(r => r.MessageId == messageId)
            .GroupBy(r => r.Emoji)
            .Select(g => new MessageReactionDto
            {
                Emoji = g.Key,
                Count = g.Count(),
                UserIds = g.Select(r => r.UserId).ToList(),
                Usernames = g.Select(r => r.Username).ToList(),
            })
            .ToListAsync(RequestToken);

        // Broadcast au groupe du salon
        await Clients.Group(message.Channel)
            .SendAsync("MessageReactionUpdated", messageId, updatedReactions, RequestToken);
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
                   .FirstOrDefaultAsync(u => u.ConnectionId == Context.ConnectionId, RequestToken);
        if (currentUser == null)
        {
            logger.LogWarning("Tentative d'envoi de photo éphémère sans utilisateur enregistré");
            return;
        }

        currentUser.LastActivity = DateTime.UtcNow;
        await db.SaveChangesAsync(RequestToken);

        var userId = currentUser.UserId;
        var userName = currentUser.Username;

        logger.LogInformation("Envoi photo éphémère de {Username} pour {Target} (privé: {IsPrivate})",
                currentUser.Username, channelOrUserId, isPrivate);

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

        if (isPrivate)
        {
            await SendPrivateEphemeralPhoto(channelOrUserId, currentUser, ephemeralPhoto, RequestToken);
            return;
        }

        var channel = await db.Channels
            .FirstOrDefaultAsync(c => c.Name.ToLower() == channelOrUserId.ToLower(), RequestToken);

        if (!await CanSendToChannelAsync(currentUser, channel, RequestToken))
        {
            return;
        }

        var isMuted = await db.MutedUsers
            .AnyAsync(m => m.ChannelName == null || (m.ChannelName.ToLower() == channelOrUserId.ToLower()
                        && m.UserId == currentUser.UserId), RequestToken);

        if (isMuted)
        {
            logger.LogInformation(
                "Photo de l'utilisateur muté {UserId} non diffusée dans {Channel}",
                currentUser.UserId,
                channelOrUserId);
            await Clients.Caller.SendAsync(ReceiveEphemeralPhoto, ephemeralPhoto, RequestToken);
            return;
        }

        await Clients.Group(channelOrUserId).SendAsync(ReceiveEphemeralPhoto, ephemeralPhoto, RequestToken);
        logger.LogInformation("Photo éphémère diffusée dans le canal {Channel}", channelOrUserId);
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var connectionId = Context.ConnectionId;
        var usersInChannels = await db.ConnectedUsers
            .Where(u => u.ConnectionId == connectionId)
            .ToListAsync(RequestToken);

        if (usersInChannels.Count != 0)
        {
            var username = usersInChannels[0].Username;
            var userId = usersInChannels[0].UserId;
            foreach (var userInChannel in from userInChannel in usersInChannels
                                          where !string.IsNullOrEmpty(userInChannel.Channel)
                                          select userInChannel.Channel)
            {
                await Groups.RemoveFromGroupAsync(connectionId, userInChannel, RequestToken);
                await Clients.Group(userInChannel)
                                    .SendAsync("UserLeft", username, userId, userInChannel, RequestToken);
            }

            db.ConnectedUsers.RemoveRange(usersInChannels);
            await db.SaveChangesAsync(RequestToken);

            var hasOtherConnections = await db.ConnectedUsers
                .AnyAsync(u => u.Username == username, RequestToken);

            if (!hasOtherConnections)
            {
                await Clients.All.SendAsync(UserStatusChangedMethod, username, userId, false, RequestToken);
                logger.LogInformation("Utilisateur {Username} complètement déconnecté", username);
            }
        }

        await base.OnDisconnectedAsync(exception);
    }

    private async Task SendPrivateEphemeralPhoto(string channelOrUserId, ConnectedUser currentUser, EphemeralPhotoDto ephemeralPhoto, CancellationToken cancellationToken)
    {
        var recipient = await db.ConnectedUsers
            .Where(u => u.UserId == channelOrUserId)
            .OrderByDescending(u => u.LastActivity)
            .FirstOrDefaultAsync(cancellationToken);

        if (!await CanSendMessageToRecipientAsync(recipient, currentUser, cancellationToken))
        {
            return;
        }

        var isGlobalyMute = await db.MutedUsers
            .Where(m => m.ChannelName == null
                && (m.UserId == currentUser.UserId || m.UserId == recipient!.UserId))
            .AnyAsync(cancellationToken);

        if (!isGlobalyMute)
        {
            await Clients.Client(recipient!.ConnectionId).SendAsync(ReceiveEphemeralPhoto, ephemeralPhoto, cancellationToken);
            logger.LogInformation("Photo éphémère envoyée en privé à {Recipient}", channelOrUserId);
        }

        await Clients.Caller.SendAsync(ReceiveEphemeralPhoto, ephemeralPhoto, cancellationToken);
    }

    private async Task<bool> CanSendMessageToRecipientAsync(ConnectedUser? recipient, ConnectedUser sender, CancellationToken cancellationToken)
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

        var hasConversation = await db.PrivateMessages
            .AnyAsync(m =>
                ((m.SenderUserId == recipient.UserId && m.RecipientUserId == sender.UserId) ||
                    (m.SenderUserId == sender.UserId && m.RecipientUserId == recipient.UserId))
                && !(m.SenderUserId == recipient.UserId && m.IsDeletedBySender)
                && !(m.RecipientUserId == recipient.UserId && m.IsDeletedByRecipient), cancellationToken);

        if (!hasConversation)
        {
            logger.LogInformation(
                "Message privé bloqué: {Sender} -> {Recipient} (destinataire en mode no PV)",
                sender.Username,
                recipient.Username);

            await Clients.Caller.SendAsync(
                "MessageBlocked",
                "Cet utilisateur ne reçoit pas de messages privés non sollicités.",
                cancellationToken);
            return false;
        }

        return true;
    }

    private async Task<bool> CanSendToChannelAsync(ConnectedUser connectedUser, Channel? channel, CancellationToken cancellationToken)
    {
        if (channel != null && channel.IsMuted)
        {
            var user = await db.ReservedUsernames
                .FirstOrDefaultAsync(u => u.Username.ToLower() == connectedUser.Username.ToLower(), cancellationToken);

            var isCreator = channel.CreatedBy.Equals(connectedUser.Username, StringComparison.OrdinalIgnoreCase);
            var isAdmin = user?.IsAdmin ?? false;

            if (!isCreator && !isAdmin)
            {
                await Clients.Caller.SendAsync(
                    "MessageBlocked",
                    "Ce salon est actuellement muet. Seul le créateur ou un administrateur peut envoyer des messages.",
                    cancellationToken);
                return false;
            }
        }

        return true;
    }
}