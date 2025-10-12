using IrcChat.Api.Data;
using IrcChat.Api.Hubs;
using IrcChat.Api.Services;
using IrcChat.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace IrcChat.Api.Extensions;

public static class WebApplicationExtensions
{
    public static async Task<WebApplication> InitializeDatabaseAsync(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ChatDbContext>();
        var authService = scope.ServiceProvider.GetRequiredService<AuthService>();

        try
        {
            // Appliquer les migrations automatiquement
            await db.Database.MigrateAsync();
            Console.WriteLine("✅ Base de données PostgreSQL migrée avec succès");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Erreur lors de la migration de la base de données: {ex.Message}");
            throw;
        }

        // Créer un admin par défaut si aucun n'existe
        if (!await db.Admins.AnyAsync())
        {
            await authService.CreateAdmin("admin", "admin123");
            Console.WriteLine("✅ Admin par défaut créé: admin / admin123");
        }

        return app;
    }

    public static WebApplication ConfigurePipeline(this WebApplication app)
    {
        // Swagger en développement uniquement
        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI(options =>
            {
                options.SwaggerEndpoint("/swagger/v1/swagger.json", "IRC Chat API v1");
                options.RoutePrefix = "swagger";
            });
        }

        // Pipeline middleware
        app.UseCors("AllowBlazor");
        app.UseHttpsRedirection();
        app.UseAuthentication();
        app.UseAuthorization();

        return app;
    }

    public static WebApplication MapApiEndpoints(this WebApplication app)
    {
        app.MapAuthEndpoints()
           .MapMessageEndpoints()
           .MapChannelEndpoints()
           .MapAdminEndpoints()
           .MapSignalRHub();

        return app;
    }

    private static WebApplication MapAuthEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/auth")
            .WithTags("Authentication");

        group.MapPost("/login", LoginAsync)
            .WithName("Login")
            .WithOpenApi();

        return app;
    }

    private static WebApplication MapMessageEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/messages")
            .WithTags("Messages");

        group.MapGet("/{channel}", GetMessagesAsync)
            .WithName("GetMessages")
            .WithOpenApi();

        group.MapPost("", SendMessageAsync)
            .WithName("SendMessage")
            .WithOpenApi();

        return app;
    }

    private static WebApplication MapChannelEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/channels")
            .WithTags("Channels");

        group.MapGet("", GetChannelsAsync)
            .WithName("GetChannels")
            .WithOpenApi();

        group.MapPost("", CreateChannelAsync)
            .WithName("CreateChannel")
            .WithOpenApi();

        group.MapGet("/{channel}/users", GetConnectedUsersAsync)
            .WithName("GetConnectedUsers")
            .WithOpenApi();

        return app;
    }

    private static WebApplication MapAdminEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/admin")
            .WithTags("Admin")
            .RequireAuthorization();

        group.MapDelete("/messages/{id}", DeleteMessageAsync)
            .WithName("DeleteMessage")
            .WithOpenApi();

        group.MapGet("/stats", GetStatsAsync)
            .WithName("GetStats")
            .WithOpenApi();

        group.MapGet("/users", GetUsersAsync)
            .WithName("GetUsers")
            .WithOpenApi();

        group.MapGet("/connected-users", GetAllConnectedUsersAsync)
            .WithName("GetAllConnectedUsers")
            .WithOpenApi();

        group.MapDelete("/connected-users/cleanup", CleanupInactiveUsersAsync)
            .WithName("CleanupInactiveUsers")
            .WithOpenApi();

        return app;
    }

    private static WebApplication MapSignalRHub(this WebApplication app)
    {
        app.MapHub<ChatHub>("/chathub");
        return app;
    }

    // ===== HANDLERS AUTHENTICATION =====

    private static async Task<IResult> LoginAsync(
        LoginRequest req,
        AuthService auth)
    {
        var admin = await auth.ValidateAdmin(req.Username, req.Password);
        if (admin == null)
            return Results.Unauthorized();

        var token = auth.GenerateToken(admin);
        return Results.Ok(new LoginResponse { Token = token, Username = admin.Username });
    }

    // ===== HANDLERS MESSAGES =====

    private static async Task<IResult> GetMessagesAsync(
        string channel,
        ChatDbContext db)
    {
        var messages = await db.Messages
            .Where(m => m.Channel == channel && !m.IsDeleted)
            .OrderByDescending(m => m.Timestamp)
            .Take(100)
            .OrderBy(m => m.Timestamp)
            .ToListAsync();

        return Results.Ok(messages);
    }

    private static async Task<IResult> SendMessageAsync(
        SendMessageRequest req,
        ChatDbContext db)
    {
        var msg = new Message
        {
            Id = Guid.NewGuid(),
            Username = req.Username,
            Content = req.Content,
            Channel = req.Channel,
            Timestamp = DateTime.UtcNow,
            IsDeleted = false
        };

        db.Messages.Add(msg);
        await db.SaveChangesAsync();

        return Results.Created($"/api/messages/{msg.Id}", msg);
    }

    // ===== HANDLERS CHANNELS =====

    private static async Task<IResult> GetChannelsAsync(ChatDbContext db)
    {
        var channels = await db.Channels.OrderBy(c => c.Name).ToListAsync();
        return Results.Ok(channels);
    }

    private static async Task<IResult> CreateChannelAsync(
        Channel channel,
        ChatDbContext db)
    {
        channel.Id = Guid.NewGuid();
        channel.CreatedAt = DateTime.UtcNow;
        db.Channels.Add(channel);
        await db.SaveChangesAsync();
        return Results.Created($"/api/channels/{channel.Id}", channel);
    }

    private static async Task<IResult> GetConnectedUsersAsync(
        string channel,
        ChatDbContext db)
    {
        var users = await db.ConnectedUsers
            .Where(u => u.Channel == channel)
            .OrderBy(u => u.Username)
            .Select(u => new User
            {
                Username = u.Username,
                ConnectedAt = u.ConnectedAt,
                ConnectionId = u.ConnectionId
            })
            .ToListAsync();

        return Results.Ok(users);
    }

    // ===== HANDLERS ADMIN =====

    private static async Task<IResult> DeleteMessageAsync(
        Guid id,
        ChatDbContext db)
    {
        var msg = await db.Messages.FindAsync(id);
        if (msg == null)
            return Results.NotFound();

        msg.IsDeleted = true;
        await db.SaveChangesAsync();

        return Results.Ok();
    }

    private static async Task<IResult> GetStatsAsync(ChatDbContext db)
    {
        var stats = new
        {
            TotalMessages = await db.Messages.CountAsync(),
            TotalChannels = await db.Channels.CountAsync(),
            MessagesToday = await db.Messages.CountAsync(m => m.Timestamp.Date == DateTime.UtcNow.Date),
            ActiveChannels = await db.Messages
                .Where(m => m.Timestamp > DateTime.UtcNow.AddHours(-24))
                .Select(m => m.Channel)
                .Distinct()
                .CountAsync()
        };

        return Results.Ok(stats);
    }

    private static async Task<IResult> GetUsersAsync(ChatDbContext db)
    {
        var users = await db.Messages
            .GroupBy(m => m.Username)
            .Select(g => new { Username = g.Key, MessageCount = g.Count() })
            .OrderByDescending(u => u.MessageCount)
            .ToListAsync();

        return Results.Ok(users);
    }

    private static async Task<IResult> GetAllConnectedUsersAsync(ChatDbContext db)
    {
        var users = await db.ConnectedUsers
            .GroupBy(u => u.Channel)
            .Select(g => new
            {
                Channel = g.Key,
                Users = g.Select(u => new
                {
                    u.Username,
                    u.ConnectedAt,
                    u.LastActivity
                }).ToList(),
                Count = g.Count()
            })
            .ToListAsync();

        return Results.Ok(users);
    }

    private static async Task<IResult> CleanupInactiveUsersAsync(ChatDbContext db)
    {
        var inactiveThreshold = DateTime.UtcNow.AddMinutes(-30);

        var inactiveUsers = await db.ConnectedUsers
            .Where(u => u.LastActivity < inactiveThreshold)
            .ToListAsync();

        if (inactiveUsers.Any())
        {
            db.ConnectedUsers.RemoveRange(inactiveUsers);
            await db.SaveChangesAsync();
        }

        return Results.Ok(new { Removed = inactiveUsers.Count });
    }
}
