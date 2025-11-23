using System.Diagnostics.CodeAnalysis;
using System.Security.Claims;
using IrcChat.Api.Authorization;
using IrcChat.Api.Data;
using IrcChat.Api.Hubs;
using IrcChat.Shared.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace IrcChat.Api.Endpoints;

[SuppressMessage("Performance", "CA1862", Justification = "Not needed in SQL")]
public static class MutedUsersEndpoints
{
    public static WebApplication MapMutedUsersEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/channels/{channelName}/muted-users")
            .WithTags("Muted Users");

        // Obtenir la liste des utilisateurs mutés dans un salon (avec leurs infos)
        group.MapGet("", GetMutedUsersAsync)
            .WithName("GetMutedUsers");

        // Muter un utilisateur (nécessite d'être créateur ou admin)
        group.MapPost("/{userId}", MuteUserAsync)
            .RequireAuthorization(AuthorizationPolicies.CanModifyChannel)
            .WithName("MuteUser");

        // Unmuter un utilisateur (nécessite d'être créateur ou admin)
        group.MapDelete("/{userId}", UnmuteUserAsync)
            .RequireAuthorization(AuthorizationPolicies.CanModifyChannel)
            .WithName("UnmuteUser");

        // Vérifier si un utilisateur est mute dans un salon
        group.MapGet("/{userId}/is-muted", IsUserMutedAsync)
            .WithName("IsUserMuted");

        return app;
    }

    private static async Task<IResult> GetMutedUsersAsync(
        string channelName,
        ChatDbContext db)
    {
        var mutedUsers = await db.MutedUsers
            .Where(m => m.ChannelName.ToLower() == channelName.ToLower())
            .OrderBy(m => m.MutedAt)
            .ToListAsync();

        // Enrichir avec les informations des utilisateurs
        var userIds = mutedUsers.Select(m => m.UserId).ToList();
        var mutedByIds = mutedUsers.Select(m => m.MutedByUserId).ToList();
        var allIds = userIds.Concat(mutedByIds).Distinct().ToList();

        var userInfos = await db.ConnectedUsers
            .Where(u => allIds.Contains(u.UserId))
            .Select(u => new { u.UserId, u.Username })
            .ToListAsync();

        var userInfoDict = userInfos.ToDictionary(u => u.UserId, u => u.Username);

        var result = mutedUsers.Select(m => new MutedUserResponse
        {
            UserId = m.UserId,
            Username = userInfoDict.TryGetValue(m.UserId, out var username) ? username : "Unknown",
            MutedByUserId = m.MutedByUserId,
            MutedByUsername = userInfoDict.TryGetValue(m.MutedByUserId, out var mutedBy) ? mutedBy : "Unknown",
            MutedAt = m.MutedAt,
            Reason = m.Reason
        }).ToList();

        return Results.Ok(result);
    }

    private static async Task<IResult> MuteUserAsync(
        string channelName,
        string userId,
        ChatDbContext db,
        HttpContext context,
        IHubContext<ChatHub> hubContext,
        MuteUserRequest? request = null)
    {
        // Vérifier que le salon existe
        var channel = await db.Channels
            .FirstOrDefaultAsync(c => c.Name.ToLower() == channelName.ToLower());

        if (channel == null)
        {
            return Results.NotFound(new { error = "channel_not_found" });
        }

        var currentUserId = context.User.Claims
            .FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrEmpty(currentUserId))
        {
            return Results.Unauthorized();
        }

        // Empêcher de se muter soi-même
        if (userId.Equals(currentUserId, StringComparison.OrdinalIgnoreCase))
        {
            return Results.BadRequest(new { error = "cannot_mute_self" });
        }

        // Vérifier si l'utilisateur est déjà mute
        var existingMute = await db.MutedUsers
            .FirstOrDefaultAsync(m => m.ChannelName.ToLower() == channelName.ToLower()
                                   && m.UserId == userId);

        if (existingMute != null)
        {
            return Results.BadRequest(new { error = "user_already_muted" });
        }

        // Créer l'entrée de mute
        var mutedUser = new MutedUser
        {
            Id = Guid.NewGuid(),
            ChannelName = channelName,
            UserId = userId,
            MutedByUserId = currentUserId,
            MutedAt = DateTime.UtcNow,
            Reason = request?.Reason
        };

        db.MutedUsers.Add(mutedUser);
        await db.SaveChangesAsync();

        // Récupérer les usernames pour les logs et notifications
        var targetUser = await db.ConnectedUsers
            .Where(u => u.UserId == userId)
            .Select(u => u.Username)
            .FirstOrDefaultAsync();

        var currentUser = await db.ReservedUsernames
            .Where(u => u.Id.ToString() == currentUserId)
            .Select(u => u.Username)
            .FirstOrDefaultAsync();

        // Notifier tous les clients du salon SAUF l'utilisateur mute
        // L'utilisateur mute ne doit pas savoir qu'il est mute
        await hubContext.Clients.GroupExcept(channelName, GetUserConnectionIds(db, userId).Result)
            .SendAsync("UserMuted", channelName, userId, targetUser, currentUserId, currentUser);

        return Results.Ok(new
        {
            channelName,
            userId,
            username = targetUser,
            mutedByUserId = currentUserId,
            mutedByUsername = currentUser,
            mutedAt = mutedUser.MutedAt,
            reason = mutedUser.Reason
        });
    }

    private static async Task<IResult> UnmuteUserAsync(
        string channelName,
        string userId,
        ChatDbContext db,
        HttpContext context,
        IHubContext<ChatHub> hubContext)
    {
        var currentUserId = context.User.Claims
            .FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrEmpty(currentUserId))
        {
            return Results.Unauthorized();
        }

        var mutedUser = await db.MutedUsers
            .FirstOrDefaultAsync(m => m.ChannelName.ToLower() == channelName.ToLower()
                                   && m.UserId == userId);

        if (mutedUser == null)
        {
            return Results.NotFound(new { error = "user_not_muted" });
        }

        // Empêcher un utilisateur de se dé-muter lui-même
        if (userId.Equals(currentUserId, StringComparison.OrdinalIgnoreCase))
        {
            return Results.BadRequest(new { error = "cannot_unmute_self" });
        }

        db.MutedUsers.Remove(mutedUser);
        await db.SaveChangesAsync();

        // Récupérer les usernames pour les notifications
        var targetUser = await db.ReservedUsernames
            .Where(u => u.Id.ToString() == userId)
            .Select(u => u.Username)
            .FirstOrDefaultAsync();

        var currentUser = await db.ReservedUsernames
            .Where(u => u.Id.ToString() == currentUserId)
            .Select(u => u.Username)
            .FirstOrDefaultAsync();

        // Notifier tous les clients du salon
        await hubContext.Clients.Group(channelName)
            .SendAsync("UserUnmuted", channelName, userId, targetUser, currentUserId, currentUser);

        return Results.Ok(new
        {
            channelName,
            userId,
            username = targetUser,
            unmutedByUserId = currentUserId,
            unmutedByUsername = currentUser
        });
    }

    private static async Task<IResult> IsUserMutedAsync(
        string channelName,
        string userId,
        ChatDbContext db)
    {
        var isMuted = await db.MutedUsers
            .AnyAsync(m => m.ChannelName.ToLower() == channelName.ToLower()
                        && m.UserId == userId);

        return Results.Ok(new { userId, channelName, isMuted });
    }

    /// <summary>
    /// Récupère tous les ConnectionIds d'un utilisateur (peut avoir plusieurs connexions)
    /// </summary>
    private static async Task<List<string>> GetUserConnectionIds(ChatDbContext db, string userId)
    {
        return await db.ConnectedUsers
            .Where(u => u.UserId == userId)
            .Select(u => u.ConnectionId)
            .ToListAsync();
    }
}