using IrcChat.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace IrcChat.Api.Endpoints;

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
        ChatDbContext db)
    {
        // Récupérer tous les messages non supprimés du salon des utilisateurs non mutés
        var messages = await db.Messages
            .Where(m => m.Channel == channel
                && !m.IsDeleted)
            .OrderByDescending(m => m.Timestamp)
            .Take(100)
            .OrderBy(m => m.Timestamp)
            .ToListAsync();

        return Results.Ok(messages);
    }
}