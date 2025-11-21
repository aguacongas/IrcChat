using System.Diagnostics.CodeAnalysis;
using IrcChat.Api.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IrcChat.Api.Endpoints;

[SuppressMessage("Performance", "CA1862", Justification = "Not needed in SQL")]
public static class MessageEndpoints
{
    public static WebApplication MapMessageEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/messages")
            .WithTags("Messages");

        group.MapGet("/{channel}", GetMessagesAsync)
            .WithName("GetMessages");

        return app;
    }

    private static async Task<IResult> GetMessagesAsync(
        string channel,
        [FromQuery] string userId,
        ChatDbContext db)
    {
        // Récupérer tous les messages non supprimés du salon
        var messages = await db.Messages
            .Where(m => m.Channel == channel && !m.IsDeleted)
            .OrderByDescending(m => m.Timestamp)
            .Take(100)
            .OrderBy(m => m.Timestamp)
            .ToListAsync();

        // Récupérer la liste des utilisateurs mutés dans ce salon
        var mutedUsernames = await db.MutedUsers
            .Where(m => m.ChannelName.ToLower() == channel.ToLower() && m.UserId != userId)
            .Select(m => m.UserId)
            .ToListAsync();

        // Filtrer les messages des utilisateurs mutés
        var filteredMessages = messages
            .Where(m => !mutedUsernames.Contains(m.UserId))
            .ToList();

        return Results.Ok(filteredMessages);
    }
}