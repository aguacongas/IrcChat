using System.Diagnostics.CodeAnalysis;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using IrcChat.Api.Data;
using IrcChat.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace IrcChat.Api.Tests.Integration;

public class ChannelEndpointsTests(ApiWebApplicationFactory factory) : IClassFixture<ApiWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task GetChannels_ShouldReturnChannelList()
    {
        // Act
        var response = await _client.GetAsync("/api/channels");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var channels = await response.Content.ReadFromJsonAsync<List<Channel>>();
        Assert.NotNull(channels);
    }

    [Fact]
    public async Task GetConnectedUsers_ShouldReturnUsersInChannel()
    {
        // Arrange
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ChatDbContext>();

        var channel = "test-channel-users";

        var user1 = new ConnectedUser
        {
            Id = Guid.NewGuid(),
            Username = "user1",
            Channel = channel,
            ConnectionId = "conn1",
            ConnectedAt = DateTime.UtcNow,
            LastActivity = DateTime.UtcNow,
            ServerInstanceId = "test"
        };

        var user2 = new ConnectedUser
        {
            Id = Guid.NewGuid(),
            Username = "user2",
            Channel = channel,
            ConnectionId = "conn2",
            ConnectedAt = DateTime.UtcNow,
            LastActivity = DateTime.UtcNow,
            ServerInstanceId = "test"
        };

        db.ConnectedUsers.AddRange(user1, user2);
        await db.SaveChangesAsync();

        // Act
        var response = await _client.GetAsync($"/api/channels/{channel}/users");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var users = await response.Content.ReadFromJsonAsync<List<User>>();
        Assert.NotNull(users);
        Assert.True(users.Count >= 2);
        Assert.Contains(users, u => u.Username == "user1");
        Assert.Contains(users, u => u.Username == "user2");
    }

    [Fact]
    public async Task CreateChannel_WithAuthentication_ShouldCreateChannel()
    {
        // Arrange
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ChatDbContext>();

        var reservedUser = new ReservedUsername
        {
            Id = Guid.NewGuid(),
            Username = "oauth_test_user",
            Provider = ExternalAuthProvider.Google,
            ExternalUserId = "google-test-123",
            Email = "test@example.com",
            CreatedAt = DateTime.UtcNow,
            LastLoginAt = DateTime.UtcNow,
            IsAdmin = false
        };

        db.ReservedUsernames.Add(reservedUser);
        await db.SaveChangesAsync();

        var token = GenerateOAuthToken(reservedUser);
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        var channel = new Channel
        {
            Name = Guid.NewGuid().ToString(),
            CreatedBy = reservedUser.Username
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/channels", channel);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<Channel>();
        Assert.NotNull(created);
        Assert.Equal(channel.Name, created!.Name);
        Assert.Equal(reservedUser.Username, created.CreatedBy);
    }

    [Fact]
    public async Task CreateChannel_WithoutAuthentication_ShouldReturnUnauthorized()
    {
        // Arrange
        var channel = new Channel
        {
            Name = Guid.NewGuid().ToString(),
            CreatedBy = "testuser"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/channels", channel);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CreateChannel_DuplicateName_ShouldReturnBadRequest()
    {
        // Arrange
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ChatDbContext>();

        var reservedUser = new ReservedUsername
        {
            Id = Guid.NewGuid(),
            Username = "duplicate_test_user",
            Provider = ExternalAuthProvider.Google,
            ExternalUserId = "google-dup-123",
            Email = "dup@example.com",
            CreatedAt = DateTime.UtcNow,
            LastLoginAt = DateTime.UtcNow,
            IsAdmin = false
        };

        db.ReservedUsernames.Add(reservedUser);

        var existingChannel = new Channel
        {
            Id = Guid.NewGuid(),
            Name = "duplicate-channel",
            CreatedBy = reservedUser.Username,
            CreatedAt = DateTime.UtcNow
        };

        db.Channels.Add(existingChannel);
        await db.SaveChangesAsync();

        var token = GenerateOAuthToken(reservedUser);
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        var channel = new Channel
        {
            Name = "duplicate-channel",
            CreatedBy = reservedUser.Username
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/channels", channel);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.NotNull(error);
        Assert.Equal("channel_exists", error!.Error);
    }

    [Fact]
    public async Task CreateChannel_ShouldTrimChannelName()
    {
        // Arrange
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ChatDbContext>();

        var reservedUser = new ReservedUsername
        {
            Id = Guid.NewGuid(),
            Username = "trim_test_user",
            Provider = ExternalAuthProvider.Google,
            ExternalUserId = "google-trim-123",
            Email = "trim@example.com",
            CreatedAt = DateTime.UtcNow,
            LastLoginAt = DateTime.UtcNow,
            IsAdmin = false
        };

        db.ReservedUsernames.Add(reservedUser);
        await db.SaveChangesAsync();

        var token = GenerateOAuthToken(reservedUser);
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        var channel = new Channel
        {
            Name = "  trimmed-channel  ",
            CreatedBy = reservedUser.Username
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/channels", channel);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<Channel>();
        Assert.NotNull(created);
        Assert.Equal("trimmed-channel", created!.Name);
    }

    [Fact]
    public async Task GetConnectedUsers_EmptyChannel_ShouldReturnEmptyList()
    {
        // Act
        var response = await _client.GetAsync("/api/channels/empty-channel-no-users/users");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var users = await response.Content.ReadFromJsonAsync<List<User>>();
        Assert.NotNull(users);
        Assert.Empty(users);
    }

    [Fact]
    public async Task UpdateChannel_WithValidData_ShouldUpdateDescription()
    {
        // Arrange
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ChatDbContext>();

        var channel = new Channel
        {
            Id = Guid.NewGuid(),
            Name = Guid.NewGuid().ToString(),
            Description = "Ancienne description",
            CreatedBy = "creator",
            CreatedAt = DateTime.UtcNow,
            IsMuted = false
        };
        db.Channels.Add(channel);

        var user = new ReservedUsername
        {
            Id = Guid.NewGuid(),
            Username = "creator",
            Provider = ExternalAuthProvider.Google,
            ExternalUserId = "ext-id",
            Email = "creator@test.com",
            IsAdmin = false,
            CreatedAt = DateTime.UtcNow
        };
        db.ReservedUsernames.Add(user);
        await db.SaveChangesAsync();

        var token = GenerateOAuthToken(user);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var request = new Channel { Description = "Nouvelle description" };

        // Act
        var response = await _client.PutAsJsonAsync($"/api/channels/{channel.Name}", request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // Vérifier en BDD avec nouveau scope
        using var verifyScope = factory.Services.CreateScope();
        using var verifyDb = verifyScope.ServiceProvider.GetRequiredService<ChatDbContext>();

        var updatedChannel = await verifyDb.Channels.FindAsync(channel.Id);
        Assert.NotNull(updatedChannel);
        Assert.Equal("Nouvelle description", updatedChannel.Description);
    }

    [Fact]
    public async Task UpdateChannel_WithNonExistentChannel_ShouldReturnNotFound()
    {
        // Arrange
        var user = new ReservedUsername
        {
            Id = Guid.NewGuid(),
            Username = "admin",
            Provider = ExternalAuthProvider.Google,
            ExternalUserId = "ext-id",
            Email = "admin@test.com",
            IsAdmin = true,
            CreatedAt = DateTime.UtcNow
        };

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ChatDbContext>();
        db.ReservedUsernames.Add(user);
        await db.SaveChangesAsync();

        var token = GenerateOAuthToken(user);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var request = new Channel { Description = "Description" };

        // Act
        var response = await _client.PutAsJsonAsync("/api/channels/nonexistent", request);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task UpdateChannel_WithoutAuthorization_ShouldReturnUnauthorized()
    {
        // Arrange
        var request = new Channel { Description = "Description" };

        // Act
        var response = await _client.PutAsJsonAsync("/api/channels/test-channel", request);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task UpdateChannel_ByNonCreatorNonAdmin_ShouldReturnForbidden()
    {
        // Arrange
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ChatDbContext>();

        var channel = new Channel
        {
            Id = Guid.NewGuid(),
            Name = Guid.NewGuid().ToString(),
            Description = "Description",
            CreatedBy = "creator",
            CreatedAt = DateTime.UtcNow,
            IsMuted = false
        };
        db.Channels.Add(channel);

        var creator = new ReservedUsername
        {
            Id = Guid.NewGuid(),
            Username = "creator",
            Provider = ExternalAuthProvider.Google,
            ExternalUserId = "ext-id-1",
            Email = "creator@test.com",
            IsAdmin = false,
            CreatedAt = DateTime.UtcNow
        };

        var otherUser = new ReservedUsername
        {
            Id = Guid.NewGuid(),
            Username = "otheruser",
            Provider = ExternalAuthProvider.Google,
            ExternalUserId = "ext-id-2",
            Email = "other@test.com",
            IsAdmin = false,
            CreatedAt = DateTime.UtcNow
        };

        db.ReservedUsernames.AddRange(creator, otherUser);
        await db.SaveChangesAsync();

        var token = GenerateOAuthToken(otherUser);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var request = new Channel { Description = "Nouvelle description" };

        // Act
        var response = await _client.PutAsJsonAsync($"/api/channels/{channel.Name}", request);

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task UpdateChannel_ByAdmin_ShouldSucceed()
    {
        // Arrange
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ChatDbContext>();

        var channel = new Channel
        {
            Id = Guid.NewGuid(),
            Name = Guid.NewGuid().ToString(),
            Description = "Ancienne description",
            CreatedBy = "creator",
            CreatedAt = DateTime.UtcNow,
            IsMuted = false
        };
        db.Channels.Add(channel);

        var admin = new ReservedUsername
        {
            Id = Guid.NewGuid(),
            Username = "admin",
            Provider = ExternalAuthProvider.Google,
            ExternalUserId = "ext-id",
            Email = "admin@test.com",
            IsAdmin = true,
            CreatedAt = DateTime.UtcNow
        };

        db.ReservedUsernames.Add(admin);
        await db.SaveChangesAsync();

        var token = GenerateOAuthToken(admin);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var request = new Channel
        {
            Description = "Nouvelle description par admin"
        };

        // Act
        var response = await _client.PutAsJsonAsync($"/api/channels/{channel.Name}", request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var verifyScope = factory.Services.CreateScope();
        using var verifyDb = verifyScope.ServiceProvider.GetRequiredService<ChatDbContext>();

        var updatedChannel = await verifyDb.Channels.FindAsync(channel.Id);
        Assert.NotNull(updatedChannel);
        Assert.Equal(request.Description, updatedChannel.Description);
    }

    [Fact]
    public async Task UpdateChannel_WithEmptyDescription_ShouldClearDescription()
    {
        // Arrange
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ChatDbContext>();

        var channel = new Channel
        {
            Id = Guid.NewGuid(),
            Name = Guid.NewGuid().ToString(),
            Description = "Description existante",
            CreatedBy = "creator",
            CreatedAt = DateTime.UtcNow,
            IsMuted = false
        };
        db.Channels.Add(channel);

        var user = new ReservedUsername
        {
            Id = Guid.NewGuid(),
            Username = "creator",
            Provider = ExternalAuthProvider.Google,
            ExternalUserId = "ext-id",
            Email = "creator@test.com",
            IsAdmin = false,
            CreatedAt = DateTime.UtcNow
        };
        db.ReservedUsernames.Add(user);
        await db.SaveChangesAsync();

        var token = GenerateOAuthToken(user);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var request = new Channel
        {
            Description = string.Empty
        };

        // Act
        var response = await _client.PutAsJsonAsync($"/api/channels/{channel.Name}", request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var verifyScope = factory.Services.CreateScope();
        using var verifyDb = verifyScope.ServiceProvider.GetRequiredService<ChatDbContext>();

        var updatedChannel = await verifyDb.Channels.FindAsync(channel.Id);
        Assert.NotNull(updatedChannel);
        Assert.Equal(string.Empty, updatedChannel.Description);
    }


    [Fact]
    public async Task CreateChannel_WithDescription_ShouldCreateChannelWithDescription()
    {
        // Arrange
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ChatDbContext>();

        var user = new ReservedUsername
        {
            Id = Guid.NewGuid(),
            Username = "testuser",
            Provider = ExternalAuthProvider.Google,
            ExternalUserId = "google123",
            Email = "test@example.com",
            CreatedAt = DateTime.UtcNow,
            LastLoginAt = DateTime.UtcNow,
            IsAdmin = false
        };
        db.ReservedUsernames.Add(user);
        await db.SaveChangesAsync();

        var token = GenerateOAuthToken(user);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var request = new Channel
        {
            Name = "gaming",
            Description = "Canal pour discuter de jeux vidéo"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/channels", request);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<Channel>();
        Assert.NotNull(result);
        Assert.Equal("gaming", result.Name);
        Assert.Equal("Canal pour discuter de jeux vidéo", result.Description);
        Assert.Equal("testuser", result.CreatedBy);
        Assert.False(result.IsMuted);

        // Vérifier en base de données
        using var verifyScope = factory.Services.CreateScope();
        using var verifyContext = verifyScope.ServiceProvider.GetRequiredService<ChatDbContext>();

        var savedChannel = await verifyContext.Channels
            .FirstOrDefaultAsync(c => c.Name == "gaming");

        Assert.NotNull(savedChannel);
        Assert.Equal("Canal pour discuter de jeux vidéo", savedChannel.Description);
    }

    [Fact]
    public async Task CreateChannel_WithoutDescription_ShouldCreateChannelWithNullDescription()
    {
        // Arrange
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ChatDbContext>();

        var user = new ReservedUsername
        {
            Id = Guid.NewGuid(),
            Username = "testuser",
            Provider = ExternalAuthProvider.Google,
            ExternalUserId = "google123",
            Email = "test@example.com",
            CreatedAt = DateTime.UtcNow,
            LastLoginAt = DateTime.UtcNow,
            IsAdmin = false
        };
        db.ReservedUsernames.Add(user);
        await db.SaveChangesAsync();

        var token = GenerateOAuthToken(user);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var request = new Channel
        {
            Name = "random",
            Description = null
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/channels", request);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<Channel>();
        Assert.NotNull(result);
        Assert.Equal("random", result.Name);
        Assert.Null(result.Description);

        // Vérifier en base de données
        using var verifyScope = factory.Services.CreateScope();
        using var verifyContext = verifyScope.ServiceProvider.GetRequiredService<ChatDbContext>();

        var savedChannel = await verifyContext.Channels
            .FirstOrDefaultAsync(c => c.Name == "random");

        Assert.NotNull(savedChannel);
        Assert.Null(savedChannel.Description);
    }

    [Fact]
    public async Task UpdateChannel_WithDescription_ShouldUpdateDescription()
    {
        // Arrange
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ChatDbContext>();

        var user = new ReservedUsername
        {
            Id = Guid.NewGuid(),
            Username = "creator",
            Provider = ExternalAuthProvider.Google,
            ExternalUserId = "google123",
            Email = "test@example.com",
            CreatedAt = DateTime.UtcNow,
            LastLoginAt = DateTime.UtcNow,
            IsAdmin = false
        };
        db.ReservedUsernames.Add(user);

        var channel = new Channel
        {
            Id = Guid.NewGuid(),
            Name = "tech",
            Description = "Ancienne description",
            CreatedBy = "creator",
            CreatedAt = DateTime.UtcNow,
            ActiveManager = "creator",
            IsMuted = false
        };
        db.Channels.Add(channel);
        await db.SaveChangesAsync();

        var token = GenerateOAuthToken(user);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var request = new Channel
        {
            Description = "Nouvelle description mise à jour"
        };

        // Act
        var response = await _client.PutAsJsonAsync("/api/channels/tech", request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // Vérifier en base de données
        using var verifyScope = factory.Services.CreateScope();
        using var verifyContext = verifyScope.ServiceProvider.GetRequiredService<ChatDbContext>();

        var updatedChannel = await verifyContext.Channels
            .FirstOrDefaultAsync(c => c.Name == "tech");

        Assert.NotNull(updatedChannel);
        Assert.Equal("Nouvelle description mise à jour", updatedChannel.Description);
    }

    [Fact]
    public async Task UpdateChannel_SetDescriptionToNull_ShouldClearDescription()
    {
        // Arrange
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ChatDbContext>();

        var user = new ReservedUsername
        {
            Id = Guid.NewGuid(),
            Username = "creator",
            Provider = ExternalAuthProvider.Google,
            ExternalUserId = "google123",
            Email = "test@example.com",
            CreatedAt = DateTime.UtcNow,
            LastLoginAt = DateTime.UtcNow,
            IsAdmin = false
        };
        db.ReservedUsernames.Add(user);

        var channel = new Channel
        {
            Id = Guid.NewGuid(),
            Name = "music",
            Description = "Canal de musique",
            CreatedBy = "creator",
            CreatedAt = DateTime.UtcNow,
            ActiveManager = "creator",
            IsMuted = false
        };
        db.Channels.Add(channel);
        await db.SaveChangesAsync();

        var token = GenerateOAuthToken(user);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var request = new Channel
        {
            Description = "   "
        };

        // Act
        var response = await _client.PutAsJsonAsync("/api/channels/music", request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // Vérifier en base de données
        using var verifyScope = factory.Services.CreateScope();
        using var verifyContext = verifyScope.ServiceProvider.GetRequiredService<ChatDbContext>();

        var updatedChannel = await verifyContext.Channels
            .FirstOrDefaultAsync(c => c.Name == "music");

        Assert.NotNull(updatedChannel);
        Assert.Empty(updatedChannel.Description!);
    }

    [Fact]
    public async Task UpdateChannel_WithLongDescription_ShouldTrimAndUpdate()
    {
        // Arrange
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ChatDbContext>();

        var user = new ReservedUsername
        {
            Id = Guid.NewGuid(),
            Username = "creator",
            Provider = ExternalAuthProvider.Google,
            ExternalUserId = "google123",
            Email = "test@example.com",
            CreatedAt = DateTime.UtcNow,
            LastLoginAt = DateTime.UtcNow,
            IsAdmin = false
        };
        db.ReservedUsernames.Add(user);

        var channel = new Channel
        {
            Id = Guid.NewGuid(),
            Name = "general",
            Description = null,
            CreatedBy = "creator",
            CreatedAt = DateTime.UtcNow,
            ActiveManager = "creator",
            IsMuted = false
        };
        db.Channels.Add(channel);
        await db.SaveChangesAsync();

        var token = GenerateOAuthToken(user);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var request = new Channel
        {
            Description = "   Description avec espaces   "
        };

        // Act
        var response = await _client.PutAsJsonAsync("/api/channels/general", request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // Vérifier en base de données
        using var verifyScope = factory.Services.CreateScope();
        using var verifyContext = verifyScope.ServiceProvider.GetRequiredService<ChatDbContext>();

        var updatedChannel = await verifyContext.Channels
            .FirstOrDefaultAsync(c => c.Name == "general");

        Assert.NotNull(updatedChannel);
        Assert.Equal("Description avec espaces", updatedChannel.Description);
    }

    [Fact]
    public async Task GetChannels_ShouldReturnChannelsWithDescriptions()
    {
        // Arrange
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ChatDbContext>();

        var gamingId = Guid.NewGuid();
        var musicId = Guid.NewGuid();
        var channels = new List<Channel>
        {
            new()
            {
                Id = gamingId,
                Name = "gaming",
                Description = "Pour les gamers",
                CreatedBy = "user1",
                CreatedAt = DateTime.UtcNow,
                ActiveManager = "user1",
                IsMuted = false
            },
            new()
            {
                Id = musicId,
                Name = "music",
                Description = null,
                CreatedBy = "user2",
                CreatedAt = DateTime.UtcNow,
                ActiveManager = "user2",
                IsMuted = false
            }
        };

        db.Channels.AddRange(channels);
        await db.SaveChangesAsync();

        // Act
        var response = await _client.GetAsync("/api/channels");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<List<Channel>>();
        Assert.NotNull(result);
        Assert.True(result.Count >= 2);

        var gamingChannel = result.First(c => c.Id == gamingId);
        Assert.Equal("Pour les gamers", gamingChannel.Description);

        var musicChannel = result.First(c => c.Id == musicId);
        Assert.Null(musicChannel.Description);
    }

    [SuppressMessage("Blocker Vulnerability", "S6781:JWT secret keys should not be disclosed", Justification = "That's tests")]
    private static string GenerateOAuthToken(ReservedUsername user)
    {
        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes("VotreCleSecrete123456789012345678901234567890"));

        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.Username),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim("provider", user.Provider.ToString())
        };

        var token = new JwtSecurityToken(
            issuer: "IrcChatApi",
            audience: "IrcChatClient",
            claims: claims,
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private class ErrorResponse
    {
        public string Error { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
    }
}