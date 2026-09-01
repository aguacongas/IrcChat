using System.Security.Claims;
using IrcChat.Api.Authorization;
using IrcChat.Api.Data;
using IrcChat.Api.Hubs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace IrcChat.Api.Endpoints;

public static class MessageEndpoints
{
    public static WebApplication MapMessageEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/messages")
            .WithTags("Messages");

        group.MapGet("/{channelName}", GetMessagesAsync)
            .WithName("GetMessages");

        group.MapDelete("/{channelName}/{messageId:guid}", DeleteMessageAsync)
            .RequireAuthorization(AuthorizationPolicies.CanModifyChannel)
            .WithName("DeleteMessage")
            .RequireAuthorization();

        return app;
    }

    private static async Task<IResult> GetMessagesAsync(
        string channelName,
        ChatDbContext db,
        CancellationToken cancellationToken)
    {
        // Récupérer tous les messages non supprimés du salon des utilisateurs non mutés
        var messages = await db.Messages
            .Where(m => m.Channel == channelName
                && !m.IsDeleted)
            .OrderByDescending(m => m.Timestamp)
            .Take(100)
            .OrderBy(m => m.Timestamp)
            .ToListAsync(cancellationToken);

        return Results.Ok(messages);
    }

    private static async Task<IResult> DeleteMessageAsync(
        string channelName,
        Guid messageId,
        ChatDbContext db,
        IHubContext<ChatHub> hubContext,
        ClaimsPrincipal user,
        ILogger<ChatHub> logger,
        CancellationToken cancellationToken)
    {
        var message = await db.Messages.FindAsync(messageId, cancellationToken);

        if (message != null && message.Channel == channelName)
        {
            // Marquer le message comme supprimé
            message.IsDeleted = true;
            await db.SaveChangesAsync(cancellationToken);
        }

        // Notifier tous les utilisateurs du canal
        await hubContext.Clients.Group(channelName)
            .SendAsync("MessageDeleted", messageId, channelName, cancellationToken);

        logger.LogInformation(
            "Message {MessageId} supprimé dans {Channel} par {User}",
            messageId,
            channelName,
            user.Identity?.Name);

        return Results.NoContent();
    }

}