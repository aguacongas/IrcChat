using System.Diagnostics.CodeAnalysis;
using IrcChat.Api.Data;
using IrcChat.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace IrcChat.Api.Endpoints;

[SuppressMessage("Performance", "CA1862", Justification = "Not needed in SQL")]
public static class PrivateMessageEndpoints
{
    public static WebApplication MapPrivateMessageEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/private-messages")
            .WithTags("Private Messages");

        // Récupérer les conversations
        group.MapGet("/conversations/{userId}", GetConversationsAsync)
            .WithName("GetConversations")
            .WithOpenApi();

        // Récupérer les messages d'une conversation
        group.MapGet("/{userId}/with/{otherUserId}", GetPrivateMessagesAsync)
            .WithName("GetPrivateMessages")
            .WithOpenApi();

        // Récupérer le nombre de messages non lus
        group.MapGet("/{userId}/unread-count", GetUnreadCountAsync)
            .WithName("GetUnreadCount")
            .WithOpenApi();

        // Supprimer une conversation (soft delete)
        group.MapDelete("/{userId}/conversation/{otherUserId}", DeleteConversationAsync)
            .WithName("DeleteConversation")
            .WithOpenApi();

        // Vérifier le statut de connexion d'un utilisateur
        group.MapGet("/status/{username}", GetUserStatusAsync)
            .WithName("GetUserStatus")
            .WithOpenApi();

        return app;
    }

    /// <summary>
    /// Récupère les conversations d'un utilisateur via son userId client
    /// </summary>
    private static async Task<IResult> GetConversationsAsync(
        string userId,
        ChatDbContext db,
        ILogger<WebApplication> logger)
    {
        logger.LogInformation("Récupération des conversations pour UserId {UserId}", userId);

        // Filtrer directement par UserId client
        var conversations = await db.PrivateMessages
            .Where(m => (m.SenderUserId == userId || m.RecipientUserId == userId)
                     && !m.IsDeleted)
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
    /// </summary>
    private static async Task<IResult> GetPrivateMessagesAsync(
        string userId,
        string otherUserId,
        ChatDbContext db,
        ILogger<WebApplication> logger)
    {
        logger.LogInformation(
            "Récupération des messages entre UserId {UserId} et {OtherUserId}",
            userId, otherUserId);

        // Filtrer directement par userId
        var messages = await db.PrivateMessages
            .Where(m => ((m.SenderUserId == userId && m.RecipientUserId == otherUserId) ||
                        (m.SenderUserId == otherUserId && m.RecipientUserId == userId))
                     && !m.IsDeleted)
            .OrderBy(m => m.Timestamp)
            .ToListAsync();

        logger.LogInformation("Trouvé {Count} messages", messages.Count);

        return Results.Ok(messages);
    }

    /// <summary>
    /// Récupère le nombre de messages non lus pour un userId
    /// </summary>
    private static async Task<IResult> GetUnreadCountAsync(
        string userId,
        ChatDbContext db,
        ILogger<WebApplication> logger)
    {
        // Compter directement par userId
        var count = await db.PrivateMessages
            .CountAsync(m => m.RecipientUserId == userId && !m.IsRead && !m.IsDeleted);

        logger.LogInformation("Nombre de messages non lus pour UserId {UserId}: {Count}",
            userId, count);

        return Results.Ok(new { UnreadCount = count });
    }

    /// <summary>
    /// Supprime une conversation entre deux userId
    /// </summary>
    private static async Task<IResult> DeleteConversationAsync(
        string userId,
        string otherUserId,
        ChatDbContext db,
        ILogger<WebApplication> logger)
    {
        // Récupérer tous les messages par userId
        var messages = await db.PrivateMessages
            .Where(m => ((m.SenderUserId == userId && m.RecipientUserId == otherUserId) ||
                        (m.SenderUserId == otherUserId && m.RecipientUserId == userId))
                     && !m.IsDeleted)
            .ToListAsync();

        if (messages.Count == 0)
        {
            logger.LogInformation("Aucun message à supprimer entre {UserId} et {OtherUserId}",
                userId, otherUserId);
            return Results.NotFound();
        }

        // Soft delete
        foreach (var message in messages)
        {
            message.IsDeleted = true;
        }

        await db.SaveChangesAsync();

        logger.LogInformation("Suppression de {Count} messages entre {UserId} et {OtherUserId}",
            messages.Count, userId, otherUserId);

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