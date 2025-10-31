using IrcChat.Api.Data;
using IrcChat.Api.Hubs;
using IrcChat.Api.Services;
using IrcChat.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Diagnostics.CodeAnalysis;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace IrcChat.Api.Extensions;

public static class WebApplicationExtensions
{
    public static async Task<WebApplication> InitializeDatabaseAsync(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ChatDbContext>();
        var authService = scope.ServiceProvider.GetRequiredService<AuthService>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

        try
        {
            // Appliquer les migrations automatiquement
            if (db.Database.IsRelational())
            {
                await db.Database.MigrateAsync();
            }

            logger.LogInformation("✅ Base de données PostgreSQL migrée avec succès");

            // Créer un admin par défaut si aucun n'existe
            if (!await db.Admins.AnyAsync())
            {
                await authService.CreateAdmin("admin", "admin123");
                logger.LogInformation("✅ Admin par défaut créé: admin / admin123");
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "❌ Erreur lors de l'initialisation de la base de données");
            throw;
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
        app.MapOAuthEndpoints()
           .MapAuthEndpoints()
           .MapMessageEndpoints()
           .MapChannelEndpoints()
           .MapChannelMuteEndpoints()
           .MapAdminEndpoints()
           .MapPrivateMessageEndpoints()
           .MapAdminManagementEndpoints()
           .MapSignalRHub();

        return app;
    }

    [SuppressMessage("Performance", "CA1862:Use the 'StringComparison' method overloads to perform case-insensitive string comparisons", Justification = "Not translated in SQL requests")]
    private static WebApplication MapOAuthEndpoints(this WebApplication app)
    {
        var oauth = app.MapGroup("/api/oauth");

        // Vérifier la disponibilité et le statut d'un pseudo
        oauth.MapPost("/check-username", async (UsernameCheckRequest request, ChatDbContext context) =>
        {
            var username = request.Username.ToLower();

            // Vérifier si réservé en DB
            var reservedUser = await context.ReservedUsernames
                .FirstOrDefaultAsync(r => r.Username.ToLower() == username);

            // Vérifier si utilisé actuellement (connecté)
            var currentlyUsed = await context.ConnectedUsers
                .AnyAsync(u => u.Username.ToLower() == username);

            return Results.Ok(new UsernameCheckResponse
            {
                Available = reservedUser == null && !currentlyUsed,
                IsReserved = reservedUser != null,
                ReservedProvider = reservedUser?.Provider,
                IsCurrentlyUsed = currentlyUsed
            });
        });

        // Réserver un pseudo avec OAuth
        oauth.MapPost("/reserve-username", async (
            ReserveUsernameRequest request,
            ChatDbContext context,
            OAuthService oauthService,
            IConfiguration configuration) =>
        {
            // Vérifier que le pseudo n'est pas déjà réservé
            var exists = await context.ReservedUsernames
                .AnyAsync(r => r.Username.ToLower() == request.Username.ToLower());

            if (exists)
            {
                return Results.BadRequest(new { error = "username_taken", message = "Ce pseudo est déjà réservé" });
            }

            // Échanger le code contre un token
            var tokenResponse = await oauthService.ExchangeCodeForTokenAsync(
                request.Provider,
                request.Code,
                request.RedirectUri,
                request.CodeVerifier);

            if (tokenResponse == null)
            {
                return Results.Unauthorized();
            }

            // Obtenir les infos utilisateur
            var userInfo = await oauthService.GetUserInfoAsync(request.Provider, tokenResponse.AccessToken);

            if (userInfo == null)
            {
                return Results.Unauthorized();
            }

            // Vérifier si cet utilisateur OAuth n'a pas déjà un pseudo réservé
            var existingUser = await context.ReservedUsernames
                .FirstOrDefaultAsync(r => r.Provider == request.Provider && r.ExternalUserId == userInfo.Id);

            if (existingUser != null)
            {
                return Results.BadRequest(new { error = "already_reserved", message = "Vous avez déjà un pseudo réservé", username = existingUser.Username });
            }

            // Le premier utilisateur est automatiquement admin
            var isFirstUser = !await context.ReservedUsernames.AnyAsync();

            // Créer la réservation
            var user = new ReservedUsername
            {
                Id = Guid.NewGuid(),
                Username = request.Username.Trim(),
                Provider = request.Provider,
                ExternalUserId = userInfo.Id,
                Email = userInfo.Email,
                DisplayName = userInfo.Name,
                AvatarUrl = userInfo.AvatarUrl,
                CreatedAt = DateTime.UtcNow,
                LastLoginAt = DateTime.UtcNow,
                IsAdmin = isFirstUser
            };

            context.ReservedUsernames.Add(user);
            await context.SaveChangesAsync();

            // Générer le token JWT
            var token = GenerateJwtToken(user, configuration);

            return Results.Ok(new OAuthLoginResponse
            {
                Token = token,
                Username = user.Username,
                Email = user.Email,
                AvatarUrl = user.AvatarUrl,
                UserId = user.Id,
                IsNewUser = true,
                IsAdmin = user.IsAdmin
            });
        });

        // Se connecter avec un pseudo réservé
        oauth.MapPost("/login-reserved", async (
            OAuthTokenRequest request,
            ChatDbContext context,
            OAuthService oauthService,
            IConfiguration configuration) =>
        {
            // Échanger le code contre un token
            var tokenResponse = await oauthService.ExchangeCodeForTokenAsync(
                request.Provider,
                request.Code,
                request.RedirectUri,
                request.CodeVerifier);

            if (tokenResponse == null)
            {
                return Results.Unauthorized();
            }

            // Obtenir les infos utilisateur
            var userInfo = await oauthService.GetUserInfoAsync(request.Provider, tokenResponse.AccessToken);

            if (userInfo == null)
            {
                return Results.Unauthorized();
            }

            // Chercher l'utilisateur
            var user = await context.ReservedUsernames
                .FirstOrDefaultAsync(r => r.Provider == request.Provider && r.ExternalUserId == userInfo.Id);

            if (user == null)
            {
                return Results.NotFound(new { error = "not_found", message = "Aucun pseudo réservé trouvé" });
            }

            // Mettre à jour
            user.LastLoginAt = DateTime.UtcNow;
            user.AvatarUrl = userInfo.AvatarUrl;
            await context.SaveChangesAsync();

            // Générer le token JWT
            var token = GenerateJwtToken(user, configuration);

            return Results.Ok(new OAuthLoginResponse
            {
                Token = token,
                Username = user.Username,
                Email = user.Email,
                AvatarUrl = user.AvatarUrl,
                UserId = user.Id,
                IsNewUser = false,
                IsAdmin = user.IsAdmin
            });
        });

        oauth.MapGet("/config/{provider}", (ExternalAuthProvider provider, OAuthService oauthService) =>
        {
            try
            {
                var config = oauthService.GetProviderConfig(provider);
                return Results.Ok(new
                {
                    provider,
                    config.AuthorizationEndpoint,
                    config.ClientId,
                    config.Scope
                });
            }
            catch
            {
                return Results.BadRequest("Provider not supported");
            }
        });

        oauth.MapPost("/forget-username", async (
        HttpContext context,
        ChatDbContext db, // Injection directe du DbContext
        ILogger<Program> logger) =>
        {
            var usernameClaim = context.User.Claims
                .FirstOrDefault(c => c.Type == ClaimTypes.Name);

            var username = usernameClaim?.Value;

            if (string.IsNullOrEmpty(username))
            {
                return Results.Unauthorized();
            }

            // 1. Déconnexion côté BDD (Supprimer l'utilisateur réservé)
            var userToDelete = await db.ReservedUsernames
                .FirstOrDefaultAsync(r => r.Username.ToLower() == username);

            if (userToDelete == null)
            {
                return Results.NotFound();
            }

            db.ReservedUsernames.Remove(userToDelete);
            logger.LogInformation("Utilisateur réservé {Username} supprimé de la BDD.", username);

            // Sauvegarder les suppressions
            await db.SaveChangesAsync();

            return Results.Ok();
        }).RequireAuthorization();

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
        {
            return Results.Unauthorized();
        }

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

    [SuppressMessage("Performance", "CA1862:Use the 'StringComparison' method overloads to perform case-insensitive string comparisons", Justification = "Not needed in SQL translation")]
    private static async Task<IResult> CreateChannelAsync(
        Channel channel,
        ChatDbContext db)
    {
        // Récupérer le nom d'utilisateur depuis le context de la requête
        var username = channel.CreatedBy;
        if (string.IsNullOrEmpty(username))
        {
            return Results.BadRequest(new { error = "missing_username", message = "Le nom d'utilisateur est requis" });
        }

        // Vérifier si l'utilisateur a un pseudo réservé
        var isReserved = await db.ReservedUsernames
            .AnyAsync(r => r.Username.ToLower() == username.ToLower());

        if (!isReserved)
        {
            return Results.Forbid();
        }

        // Vérifier si le canal existe déjà
        var channelExists = await db.Channels
            .AnyAsync(c => c.Name.ToLower() == channel.Name.ToLower());

        if (channelExists)
        {
            return Results.BadRequest(new { error = "channel_exists", message = "Ce canal existe déjà" });
        }

        // Créer le canal
        channel.Id = Guid.NewGuid();
        channel.CreatedAt = DateTime.UtcNow;
        channel.Name = channel.Name.Trim();

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
        {
            return Results.NotFound();
        }

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

        if (inactiveUsers.Count != 0)
        {
            db.ConnectedUsers.RemoveRange(inactiveUsers);
            await db.SaveChangesAsync();
        }

        return Results.Ok(new { Removed = inactiveUsers.Count });
    }

    
    private static string GenerateJwtToken(ReservedUsername user, IConfiguration configuration)
    {
        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(configuration["Jwt:Key"] ?? "your-secret-key-minimum-32-characters-long-for-security"));

        var credentials = new SigningCredentials(
            key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.Username),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim("provider", user.Provider.ToString())
        };

        var token = new JwtSecurityToken(
            issuer: configuration["Jwt:Issuer"],
            audience: configuration["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddDays(30),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
