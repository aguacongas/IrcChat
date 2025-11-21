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

        // Obtenir la liste des utilisateurs mutés dans un salon
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
            .Select(m => new
            {
                m.Username,
                m.UserId,
                m.MutedBy,
                m.MutedAt,
                m.Reason
            })
            .ToListAsync();

        return Results.Ok(mutedUsers);
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

        var currentUsername = context.User.Claims
            .FirstOrDefault(c => c.Type == ClaimTypes.Name)?.Value ?? string.Empty;

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
            Username = request?.Username ?? string.Empty,
            UserId = userId,
            MutedBy = currentUsername,
            MutedAt = DateTime.UtcNow,
            Reason = request?.Reason
        };

        db.MutedUsers.Add(mutedUser);
        await db.SaveChangesAsync();

        // Notifier tous les clients du salon
        await hubContext.Clients.Group(channelName)
            .SendAsync("UserMuted", channelName, userId, currentUsername);

        return Results.Ok(new
        {
            channelName,
            userId,
            mutedBy = currentUsername,
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
        var mutedUser = await db.MutedUsers
            .FirstOrDefaultAsync(m => m.ChannelName.ToLower() == channelName.ToLower()
                                   && m.UserId == userId);

        if (mutedUser == null)
        {
            return Results.NotFound(new { error = "user_not_muted" });
        }

        var currentUsername = context.User.Claims
            .FirstOrDefault(c => c.Type == ClaimTypes.Name)?.Value ?? string.Empty;

        db.MutedUsers.Remove(mutedUser);
        await db.SaveChangesAsync();

        // Notifier tous les clients du salon
        await hubContext.Clients.Group(channelName)
            .SendAsync("UserUnmuted", channelName, userId, currentUsername);

        return Results.Ok(new
        {
            channelName,
            userId,
            unmutedBy = currentUsername
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
}