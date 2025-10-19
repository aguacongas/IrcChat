using IrcChat.Api.Data;
using IrcChat.Api.Hubs;
using IrcChat.Api.Services;
using IrcChat.Shared.Models;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics.CodeAnalysis;

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
            await db.Database.EnsureDeletedAsync();
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
        app.MapOAuthEndpoints()
           .MapAuthEndpoints()
           .MapMessageEndpoints()
           .MapChannelEndpoints()
           .MapAdminEndpoints()
           .MapSignalRHub();

        return app;
    }

    [SuppressMessage("Performance", "CA1862:Use the 'StringComparison' method overloads to perform case-insensitive string comparisons", Justification = "Not translated in SQL requests")]
    private static WebApplication MapOAuthEndpoints(this WebApplication app)
    {
        var oauth = app.MapGroup("/api/oauth");

        // Endpoint pour échanger le code contre un token et créer la session
        oauth.MapPost("/token", async (
            OAuthTokenRequest request,
            ChatDbContext context,
            OAuthService oauthService,
            IConfiguration configuration) =>
        {
            // Échanger le code contre un access token (avec code_verifier)
            var tokenResponse = await oauthService.ExchangeCodeForTokenAsync(
                request.Provider,
                request.Code,
                request.RedirectUri,
                request.CodeVerifier); // AJOUT du code_verifier

            if (tokenResponse == null)
                return Results.Unauthorized();

            // Obtenir les informations utilisateur
            var userInfo = await oauthService.GetUserInfoAsync(request.Provider, tokenResponse.AccessToken);

            if (userInfo == null)
                return Results.Unauthorized();

            // Chercher ou créer l'utilisateur
            var reservedUser = await context.ReservedUsernames
                .FirstOrDefaultAsync(r => r.Provider == request.Provider && r.ExternalUserId == userInfo.Id);

            bool isNewUser = false;

            if (reservedUser == null)
            {
                reservedUser = new ReservedUsername
                {
                    Id = Guid.NewGuid(),
                    Provider = request.Provider,
                    ExternalUserId = userInfo.Id,
                    Email = userInfo.Email,
                    DisplayName = userInfo.Name,
                    AvatarUrl = userInfo.AvatarUrl,
                    CreatedAt = DateTime.UtcNow,
                    LastLoginAt = DateTime.UtcNow
                };

                context.ReservedUsernames.Add(reservedUser);
                isNewUser = true;
            }
            else
            {
                reservedUser.LastLoginAt = DateTime.UtcNow;
                reservedUser.AvatarUrl = userInfo.AvatarUrl;
            }

            await context.SaveChangesAsync();

            // Générer notre JWT pour l'application
            var appToken = GenerateJwtToken(reservedUser, isNewUser, configuration);

            return Results.Ok(new OAuthLoginResponse
            {
                Token = appToken,
                Username = reservedUser.Username,
                Email = reservedUser.Email,
                AvatarUrl = reservedUser.AvatarUrl,
                UserId = reservedUser.Id,
                IsNewUser = isNewUser
            });
        });

        oauth.MapPost("/complete-profile", async (
            CompleteProfileRequest request,
            ChatDbContext context,
            IConfiguration configuration,
            HttpContext httpContext) =>
        {
            var userIdClaim = httpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
            var isTempClaim = httpContext.User.FindFirst("is_temp");

            if (userIdClaim == null || isTempClaim?.Value != "True")
                return Results.Unauthorized();

            if (!Guid.TryParse(userIdClaim.Value, out var userId) || userId != request.TempUserId)
                return Results.Unauthorized();

            var usernameExists = await context.ReservedUsernames
                .AnyAsync(r => r.Username.ToLower() == request.Username.ToLower()); 

            if (usernameExists)
                return Results.BadRequest(new { error = "username_taken", message = "Ce pseudo est déjà pris" });

            if (string.IsNullOrWhiteSpace(request.Username) || request.Username.Length < 3)
                return Results.BadRequest(new { error = "invalid_username", message = "Le pseudo doit contenir au moins 3 caractères" });

            var providerClaim = httpContext.User.FindFirst("provider");
            var externalIdClaim = httpContext.User.FindFirst("external_id");
            var emailClaim = httpContext.User.FindFirst(System.Security.Claims.ClaimTypes.Email);
            var displayNameClaim = httpContext.User.FindFirst("display_name");
            var avatarUrlClaim = httpContext.User.FindFirst("avatar_url");

            if (providerClaim == null || externalIdClaim == null)
                return Results.BadRequest(new { error = "invalid_token", message = "Token invalide" });

            if (!Enum.TryParse<ExternalAuthProvider>(providerClaim.Value, out var provider))
                return Results.BadRequest(new { error = "invalid_provider", message = "Provider invalide" });

            var user = await context.ReservedUsernames
                .FindAsync(userId);

            if (user == null)
            {
                return Results.BadRequest(new { error = "invalid_user_id", message = "User not found" });
            }
            user.Username = request.Username;

            await context.SaveChangesAsync();

            var token = GenerateJwtToken(user, false, configuration);

            return Results.Ok(new OAuthLoginResponse
            {
                Token = token,
                Username = user.Username,
                Email = user.Email,
                AvatarUrl = user.AvatarUrl,
                UserId = user.Id,
                IsNewUser = false
            });
        }).RequireAuthorization();

        // Endpoint pour obtenir la configuration OAuth d'un provider
        oauth.MapGet("/config/{provider}", (ExternalAuthProvider provider, OAuthService oauthService) =>
        {
            try
            {
                var config = oauthService.GetProviderConfig(provider);
                // Ne pas exposer le client secret
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

        oauth.MapGet("/check-username/{username}", async (string username, ChatDbContext context) =>
        {
            var exists = await context.ReservedUsernames
                .AnyAsync(r => r.Username.ToLower() == username.ToLower());

            return Results.Ok(!exists);
        });

        oauth.MapPost("/update-username", async (UpdateUsernameRequest request, ChatDbContext context) =>
        {
            var user = await context.ReservedUsernames
                .FirstOrDefaultAsync(r => r.Id == request.UserId);

            if (user == null)
                return Results.NotFound();

            var exists = await context.ReservedUsernames
                .AnyAsync(r => r.Username.ToLower() == request.NewUsername.ToLower() && r.Id != request.UserId);

            if (exists)
                return Results.BadRequest("Ce pseudo est déjà pris");

            user.Username = request.NewUsername;
            await context.SaveChangesAsync();

            return Results.Ok();
        });

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

        if (inactiveUsers.Count != 0)
        {
            db.ConnectedUsers.RemoveRange(inactiveUsers);
            await db.SaveChangesAsync();
        }

        return Results.Ok(new { Removed = inactiveUsers.Count });
    }

    private static string GenerateUsername(string baseName)
    {
        var username = new string([.. baseName
            .ToLower()
            .Where(c => char.IsLetterOrDigit(c) || c == '_')
            .Take(20)]);

        return string.IsNullOrEmpty(username) ? "user" : username;
    }

    private static async Task<string> GetUniqueUsername(string baseUsername, ChatDbContext context)
    {
        var username = baseUsername;
        var counter = 1;

        while (await context.ReservedUsernames.AnyAsync(r => r.Username == username))
        {
            username = $"{baseUsername}{counter}";
            counter++;
        }

        return username;
    }

    private static string GenerateJwtToken(ReservedUsername user, bool isTemp, IConfiguration configuration)
    {
        using var hmac = new System.Security.Cryptography.HMACSHA256(
            System.Text.Encoding.UTF8.GetBytes(configuration["Jwt:Key"] ?? "your-secret-key-minimum-32-characters-long-for-security"));

        var key = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(
            System.Text.Encoding.UTF8.GetBytes(configuration["Jwt:Key"] ?? "your-secret-key-minimum-32-characters-long-for-security"));

        var credentials = new Microsoft.IdentityModel.Tokens.SigningCredentials(
            key, Microsoft.IdentityModel.Tokens.SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier, user.Id.ToString()),
            new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Name, user.Username),
            new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Email, user.Email),
            new System.Security.Claims.Claim("provider", user.Provider.ToString()),
            new System.Security.Claims.Claim("external_id", user.ExternalUserId.ToString()),
            new System.Security.Claims.Claim("is_temp", isTemp.ToString())
        };

        var token = new System.IdentityModel.Tokens.Jwt.JwtSecurityToken(
            issuer: configuration["Jwt:Issuer"],
            audience: configuration["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddDays(30),
            signingCredentials: credentials
        );

        return new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler().WriteToken(token);
    }
}
