using System.Diagnostics.CodeAnalysis;
using IrcChat.Api.Authorization;
using IrcChat.Api.Data;
using IrcChat.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace IrcChat.Api.Endpoints;

[SuppressMessage("Performance", "CA1862", Justification = "Not needed in SQL")]
[SuppressMessage("Performance", "CA1873:Avoid potentially expensive logging", Justification = "Acceptable")]
public static class PrivateMessageEndpoints
{
    public static WebApplication MapPrivateMessageEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/private-messages")
            .WithTags("Private Messages");

        // Récupérer les conversations
        group.MapGet("/conversations/{userId}", GetConversationsAsync)
            .RequireAuthorization(AuthorizationPolicies.UserIdMatch)
            .WithName("GetConversations");

        // Récupérer les messages d'une conversation
        group.MapGet("/{userId}/with/{otherUserId}", GetPrivateMessagesAsync)
            .RequireAuthorization(AuthorizationPolicies.UserIdMatch)
            .WithName("GetPrivateMessages");

        // Récupérer le nombre de messages non lus
        group.MapGet("/{userId}/unread-count", GetUnreadCountAsync)
            .RequireAuthorization(AuthorizationPolicies.UserIdMatch)
            .WithName("GetUnreadCount");

        // Supprimer une conversation (soft delete pour l'utilisateur uniquement)
        group.MapDelete("/{userId}/conversation/{otherUserId}", DeleteConversationAsync)
            .RequireAuthorization(AuthorizationPolicies.UserIdMatch)
            .WithName("DeleteConversation");

        // Vérifier le statut de connexion d'un utilisateur
        group.MapGet("/status/{username}", GetUserStatusAsync)
            .WithName("GetUserStatus");

        return app;
    }

    /// <summary>
    /// Récupère les conversations d'un utilisateur via son userId client
    /// Filtre les messages supprimés par cet utilisateur
    /// </summary>
    private static async Task<IResult> GetConversationsAsync(
        string userId,
        ChatDbContext db,
        ILogger<Program> logger)
    {
        logger.LogInformation("Récupération des conversations pour UserId {UserId}", userId);

        // Filtrer directement par UserId client et exclure les messages supprimés par cet utilisateur
        var conversations = await db.PrivateMessages
            .Where(m => (m.SenderUserId == userId || m.RecipientUserId == userId)
                     && !(m.SenderUserId == userId && m.IsDeletedBySender)
                     && !(m.RecipientUserId == userId && m.IsDeletedByRecipient))
            .GroupBy(m => m.SenderUserId == userId ? m.RecipientUsername : m.SenderUsername)
            .Select(g => new
            {
                OtherUsername = g.Key,
                // Récupérer le UserId de l'autre personne depuis le dernier message
                OtherUserId = g.OrderByDescending(m => m.Timestamp)
                    .Select(m => m.SenderUserId == userId ? m.RecipientUserId : m.SenderUserId)
                    .First(),
                LastMessage = g.OrderByDescending(m => m.Timestamp).First(),
                UnreadCount = g.Count(m => m.RecipientUserId == userId && !m.IsRead)
            })
            .ToListAsync();

        // Récupérer les statuts de connexion
        var usernames = conversations.Select(c => c.OtherUsername).ToList();
        var onlineUsers = await db.ConnectedUsers
            .Where(u => usernames.Contains(u.Username))
            .Select(u => u.Username)
            .Distinct()
            .ToListAsync();

        var result = conversations.Select(c => new PrivateConversation
        {
            OtherUser = new User { UserId = c.OtherUserId, Username = c.OtherUsername },
            LastMessage = c.LastMessage.Content,
            LastMessageTime = c.LastMessage.Timestamp,
            UnreadCount = c.UnreadCount,
            IsOnline = onlineUsers.Contains(c.OtherUsername)
        }).OrderByDescending(c => c.LastMessageTime);

        logger.LogInformation("Trouvé {Count} conversations pour UserId {UserId}",
            conversations.Count, userId);

        return Results.Ok(result);
    }

    /// <summary>
    /// Récupère les messages entre deux userId
    /// Filtre les messages supprimés par l'utilisateur demandeur
    /// </summary>
    private static async Task<IResult> GetPrivateMessagesAsync(
        string userId,
        string otherUserId,
        ChatDbContext db,
        ILogger<Program> logger)
    {
        logger.LogInformation(
            "Récupération des messages entre UserId {UserId} et {OtherUserId}",
            userId, otherUserId);

        // Filtrer directement par userId et exclure les messages supprimés par cet utilisateur
        var messages = await db.PrivateMessages
            .Where(m => ((m.SenderUserId == userId && m.RecipientUserId == otherUserId) ||
                        (m.SenderUserId == otherUserId && m.RecipientUserId == userId))
                     && !(m.SenderUserId == userId && m.IsDeletedBySender)
                     && !(m.RecipientUserId == userId && m.IsDeletedByRecipient))
            .OrderBy(m => m.Timestamp)
            .ToListAsync();

        logger.LogInformation("Trouvé {Count} messages", messages.Count);

        return Results.Ok(messages);
    }

    /// <summary>
    /// Récupère le nombre de messages non lus pour un userId
    /// Exclut les messages supprimés par cet utilisateur
    /// </summary>
    private static async Task<IResult> GetUnreadCountAsync(
        string userId,
        ChatDbContext db,
        ILogger<Program> logger)
    {
        // Compter directement par userId et exclure les messages supprimés
        var count = await db.PrivateMessages
            .CountAsync(m => m.RecipientUserId == userId
                          && !m.IsRead
                          && !m.IsDeletedByRecipient);

        logger.LogInformation("Nombre de messages non lus pour UserId {UserId}: {Count}",
            userId, count);

        return Results.Ok(new { UnreadCount = count });
    }

    /// <summary>
    /// Supprime une conversation pour l'utilisateur demandeur uniquement
    /// L'autre utilisateur peut toujours voir ses messages
    /// </summary>
    private static async Task<IResult> DeleteConversationAsync(
        string userId,
        string otherUserId,
        ChatDbContext db,
        ILogger<Program> logger)
    {
        // Récupérer tous les messages de la conversation
        var messages = await db.PrivateMessages
            .Where(m => ((m.SenderUserId == userId && m.RecipientUserId == otherUserId) ||
                        (m.SenderUserId == otherUserId && m.RecipientUserId == userId))
                     && !(m.SenderUserId == userId && m.IsDeletedBySender)
                     && !(m.RecipientUserId == userId && m.IsDeletedByRecipient))
            .ToListAsync();

        if (messages.Count == 0)
        {
            logger.LogInformation("Aucun message à supprimer entre {UserId} et {OtherUserId}",
                userId, otherUserId);
            return Results.NotFound();
        }

        // Soft delete uniquement pour l'utilisateur demandeur
        foreach (var message in messages)
        {
            if (message.SenderUserId == userId)
            {
                message.IsDeletedBySender = true;
            }
            else if (message.RecipientUserId == userId)
            {
                message.IsDeletedByRecipient = true;
            }
        }

        await db.SaveChangesAsync();

        logger.LogInformation(
            "Suppression de {Count} messages entre {UserId} et {OtherUserId} pour {CallerUserId} uniquement",
            messages.Count, userId, otherUserId, userId);

        return Results.Ok(new { Deleted = messages.Count });
    }

    private static async Task<IResult> GetUserStatusAsync(
        string username,
        ChatDbContext db)
    {
        var isOnline = await db.ConnectedUsers
            .AnyAsync(u => u.Username == username);

        return Results.Ok(new { Username = username, IsOnline = isOnline });
    }
}