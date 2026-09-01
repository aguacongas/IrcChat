using System.Security.Claims;
using IrcChat.Api.Authorization;
using IrcChat.Api.Data;
using IrcChat.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace IrcChat.Api.Endpoints;

public static class GlobalMuteEndpoints
{
    public static WebApplication MapGlobalMuteEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/admin/global-mute")
            .WithTags("Admin - Global Mute")
            .RequireAuthorization(AuthorizationPolicies.IsAdmin);

        group.MapGet(string.Empty, GetGloballyMutedUsersAsync)
            .WithName("GetGloballyMutedUsers");

        group.MapPost("/{userId}", MuteUserGloballyAsync)
            .WithName("MuteUserGlobally");

        group.MapDelete("/{userId}", UnmuteUserGloballyAsync)
            .WithName("UnmuteUserGlobally");

        group.MapGet("/{userId}/is-muted", IsUserGloballyMutedAsync)
            .WithName("IsUserGloballyMuted");

        return app;
    }

    private static async Task<IResult> GetGloballyMutedUsersAsync(
        ChatDbContext db,
        CancellationToken cancellationToken)
    {
        var globallyMutedUsers = await db.MutedUsers
            .Where(m => m.ChannelName == null)
            .OrderBy(m => m.MutedAt)
            .ToListAsync(cancellationToken);

        var userIds = globallyMutedUsers.Select(m => m.UserId).ToList();
        var mutedByIds = globallyMutedUsers.Select(m => m.MutedByUserId).ToList();
        var allIds = userIds.Concat(mutedByIds).Distinct().ToList();

        var userInfos = await db.ReservedUsernames
            .Where(u => allIds.Contains(u.Id.ToString()))
            .Select(u => new { UserId = u.Id.ToString(), u.Username })
            .ToListAsync(cancellationToken);

        var userInfoDict = userInfos.ToDictionary(u => u.UserId, u => u.Username);

        var result = globallyMutedUsers.Select(m => new MutedUserResponse
        {
            UserId = m.UserId,
            Username = userInfoDict.TryGetValue(m.UserId, out var username) ? username : "Unknown",
            MutedByUserId = m.MutedByUserId,
            MutedByUsername = userInfoDict.TryGetValue(m.MutedByUserId, out var mutedBy) ? mutedBy : "Unknown",
            MutedAt = m.MutedAt,
            Reason = m.Reason,
        }).ToList();

        return Results.Ok(result);
    }

    private static async Task<IResult> MuteUserGloballyAsync(
        string userId,
        ChatDbContext db,
        HttpContext context,
        MuteUserRequest? request = null)
    {
        var currentUserId = context.User.Claims
            .FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)!.Value;

        if (userId.Equals(currentUserId, StringComparison.OrdinalIgnoreCase))
        {
            return Results.BadRequest(new { error = "cannot_mute_self" });
        }

        var existingGlobalMute = await db.MutedUsers
            .FirstOrDefaultAsync(m => m.ChannelName == null && m.UserId == userId, context.RequestAborted);

        if (existingGlobalMute != null)
        {
            return Results.BadRequest(new { error = "user_already_globally_muted" });
        }

        var globalMute = new MutedUser
        {
            Id = Guid.NewGuid(),
            ChannelName = null,
            UserId = userId,
            MutedByUserId = currentUserId,
            MutedAt = DateTime.UtcNow,
            Reason = request?.Reason,
        };

        db.MutedUsers.Add(globalMute);
        await db.SaveChangesAsync(context.RequestAborted);

        var currentUserName = context.User.Claims
            .FirstOrDefault(c => c.Type == ClaimTypes.Name)?.Value;

        return Results.Ok(new
        {
            userId,
            mutedByUserId = currentUserId,
            mutedByUsername = currentUserName,
            mutedAt = globalMute.MutedAt,
            reason = globalMute.Reason,
            isGlobal = true,
        });
    }

    private static async Task<IResult> UnmuteUserGloballyAsync(
        string userId,
        ChatDbContext db,
        HttpContext context)
    {
        var currentUserId = context.User.Claims
            .FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;

        if (userId.Equals(currentUserId, StringComparison.OrdinalIgnoreCase))
        {
            return Results.BadRequest(new { error = "cannot_unmute_self" });
        }

        var globalMute = await db.MutedUsers
            .FirstOrDefaultAsync(m => m.ChannelName == null && m.UserId == userId, context.RequestAborted);

        if (globalMute == null)
        {
            return Results.NotFound(new { error = "user_not_globally_muted" });
        }

        var targetUser = await db.ReservedUsernames
            .FirstOrDefaultAsync(u => u.Id.ToString() == userId, context.RequestAborted);

        db.MutedUsers.Remove(globalMute);
        await db.SaveChangesAsync(context.RequestAborted);

        var currentUserName = context.User.Claims
            .FirstOrDefault(c => c.Type == ClaimTypes.Name)?.Value;

        return Results.Ok(new
        {
            userId,
            username = targetUser?.Username,
            unmutedByUserId = currentUserId,
            unmutedByUsername = currentUserName,
        });
    }

    private static async Task<IResult> IsUserGloballyMutedAsync(
        string userId,
        ChatDbContext db,
        CancellationToken cancellationToken)
    {
        var isGloballyMuted = await db.MutedUsers
            .AnyAsync(m => m.ChannelName == null && m.UserId == userId, cancellationToken);

        return Results.Ok(new MuteStatusResponse { UserId = userId, IsGloballyMuted = isGloballyMuted });
    }
}