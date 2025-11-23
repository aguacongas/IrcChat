using System.Diagnostics.CodeAnalysis;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using IrcChat.Api.Data;
using IrcChat.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace IrcChat.Api.Tests.Integration;

public class MutedUsersEndpointsTests(ApiWebApplicationFactory factory) :
    IClassFixture<ApiWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task GetMutedUsers_WithNoMutedUsers_ShouldReturnEmptyList()
    {
        // Arrange
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ChatDbContext>();

        var channel = new Channel
        {
            Id = Guid.NewGuid(),
            Name = Guid.NewGuid().ToString(),
            CreatedBy = "admin",
            CreatedAt = DateTime.UtcNow
        };
        db.Channels.Add(channel);
        await db.SaveChangesAsync();

        // Act
        var response = await _client.GetAsync($"/api/channels/{channel.Name}/muted-users");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<List<MutedUserResponse>>();
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetMutedUsers_WithMutedUsers_ShouldReturnList()
    {
        // Arrange
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ChatDbContext>();

        var creator = new ReservedUsername
        {
            Id = Guid.NewGuid(),
            Username = "creator",
            Email = "creator@test.com",
            Provider = ExternalAuthProvider.Google,
            ExternalUserId = "ext-creator",
            CreatedAt = DateTime.UtcNow,
            LastLoginAt = DateTime.UtcNow
        };

        var mutedUser = new ReservedUsername
        {
            Id = Guid.NewGuid(),
            Username = "muteduser",
            Email = "muted@test.com",
            Provider = ExternalAuthProvider.Google,
            ExternalUserId = "ext-muted",
            CreatedAt = DateTime.UtcNow,
            LastLoginAt = DateTime.UtcNow
        };

        db.ReservedUsernames.AddRange(creator, mutedUser);

        var channel = new Channel
        {
            Id = Guid.NewGuid(),
            Name = Guid.NewGuid().ToString(),
            CreatedBy = creator.Username,
            CreatedAt = DateTime.UtcNow
        };
        db.Channels.Add(channel);

        var mute = new MutedUser
        {
            Id = Guid.NewGuid(),
            ChannelName = channel.Name,
            UserId = mutedUser.Id.ToString(),
            MutedByUserId = creator.Id.ToString(),
            MutedAt = DateTime.UtcNow,
            Reason = "Spam"
        };
        db.MutedUsers.Add(mute);
        await db.SaveChangesAsync();

        // Act
        var response = await _client.GetAsync($"/api/channels/{channel.Name}/muted-users");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<List<MutedUserResponse>>();
        Assert.NotNull(result);
        Assert.Single(result);

        var firstMute = result[0];
        Assert.Equal(mutedUser.Id.ToString(), firstMute.UserId);
        Assert.Equal("muteduser", firstMute.Username);
        Assert.Equal(creator.Id.ToString(), firstMute.MutedByUserId);
        Assert.Equal("creator", firstMute.MutedByUsername);
        Assert.Equal("Spam", firstMute.Reason);
    }

    [Fact]
    public async Task MuteUser_WithoutAuthentication_ShouldReturnUnauthorized()
    {
        // Arrange
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ChatDbContext>();

        var channel = new Channel
        {
            Id = Guid.NewGuid(),
            Name = Guid.NewGuid().ToString(),
            CreatedBy = "admin",
            CreatedAt = DateTime.UtcNow
        };
        db.Channels.Add(channel);
        await db.SaveChangesAsync();

        var request = new { Reason = "Test" };

        // Act
        var response = await _client.PostAsJsonAsync(
            "/api/channels/general/muted-users/user-123",
            request);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task MuteUser_AsChannelCreator_ShouldMuteUser()
    {
        // Arrange
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ChatDbContext>();

        var creator = new ReservedUsername
        {
            Id = Guid.NewGuid(),
            Username = "creator",
            Email = "creator@test.com",
            Provider = ExternalAuthProvider.Google,
            ExternalUserId = "ext-creator",
            CreatedAt = DateTime.UtcNow,
            LastLoginAt = DateTime.UtcNow,
            IsAdmin = false
        };

        var targetUser = new ReservedUsername
        {
            Id = Guid.NewGuid(),
            Username = "target",
            Email = "target@test.com",
            Provider = ExternalAuthProvider.Google,
            ExternalUserId = "ext-target",
            CreatedAt = DateTime.UtcNow,
            LastLoginAt = DateTime.UtcNow,
            IsAdmin = false
        };

        db.ReservedUsernames.AddRange(creator, targetUser);

        var channel = new Channel
        {
            Id = Guid.NewGuid(),
            Name = Guid.NewGuid().ToString(),
            CreatedBy = creator.Username,
            CreatedAt = DateTime.UtcNow
        };
        db.Channels.Add(channel);
        await db.SaveChangesAsync();

        var token = GenerateToken(creator);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var request = new { Reason = "Spam" };

        // Act
        var response = await _client.PostAsJsonAsync(
            $"/api/channels/{channel.Name}/muted-users/{targetUser.Id}",
            request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // Verify in database with new scope
        using var verifyScope = factory.Services.CreateScope();
        using var verifyContext = verifyScope.ServiceProvider.GetRequiredService<ChatDbContext>();

        var mute = await verifyContext.MutedUsers
            .FirstOrDefaultAsync(m => m.ChannelName == channel.Name && m.UserId == targetUser.Id.ToString());

        Assert.NotNull(mute);
        Assert.Equal("Spam", mute!.Reason);
        Assert.Equal(creator.Id.ToString(), mute.MutedByUserId);
    }

    [Fact]
    public async Task MuteUser_AsAdmin_CanMuteChannelCreator()
    {
        // Arrange
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ChatDbContext>();

        var admin = new ReservedUsername
        {
            Id = Guid.NewGuid(),
            Username = "admin",
            Email = "admin@test.com",
            Provider = ExternalAuthProvider.Google,
            ExternalUserId = "ext-admin",
            CreatedAt = DateTime.UtcNow,
            LastLoginAt = DateTime.UtcNow,
            IsAdmin = true
        };

        var creator = new ReservedUsername
        {
            Id = Guid.NewGuid(),
            Username = "creator",
            Email = "creator@test.com",
            Provider = ExternalAuthProvider.Google,
            ExternalUserId = "ext-creator",
            CreatedAt = DateTime.UtcNow,
            LastLoginAt = DateTime.UtcNow,
            IsAdmin = false
        };

        db.ReservedUsernames.AddRange(admin, creator);

        var channel = new Channel
        {
            Id = Guid.NewGuid(),
            Name = Guid.NewGuid().ToString(),
            CreatedBy = creator.Username,
            CreatedAt = DateTime.UtcNow
        };
        db.Channels.Add(channel);
        await db.SaveChangesAsync();

        var token = GenerateToken(admin);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var request = new { Reason = "Violation" };

        // Act
        var response = await _client.PostAsJsonAsync(
            $"/api/channels/{channel.Name}/muted-users/{creator.Id}",
            request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // Verify in database
        using var verifyScope = factory.Services.CreateScope();
        using var verifyContext = verifyScope.ServiceProvider.GetRequiredService<ChatDbContext>();

        var mute = await verifyContext.MutedUsers
            .FirstOrDefaultAsync(m => m.UserId == creator.Id.ToString());

        Assert.NotNull(mute);
    }

    [Fact]
    public async Task MuteUser_CannotMuteSelf_ShouldReturnBadRequest()
    {
        // Arrange
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ChatDbContext>();

        var user = new ReservedUsername
        {
            Id = Guid.NewGuid(),
            Username = Guid.NewGuid().ToString(),
            Email = "user@test.com",
            Provider = ExternalAuthProvider.Google,
            ExternalUserId = "ext-user",
            CreatedAt = DateTime.UtcNow,
            LastLoginAt = DateTime.UtcNow,
            IsAdmin = false
        };

        db.ReservedUsernames.Add(user);

        var channel = new Channel
        {
            Id = Guid.NewGuid(),
            Name = Guid.NewGuid().ToString(),
            CreatedBy = user.Username,
            CreatedAt = DateTime.UtcNow
        };
        db.Channels.Add(channel);
        await db.SaveChangesAsync();

        var token = GenerateToken(user);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var request = new { Reason = "Test" };

        // Act
        var response = await _client.PostAsJsonAsync(
            $"/api/channels/{channel.Name}/muted-users/{user.Id}",
            request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();
        Assert.NotNull(error);
        Assert.Equal("cannot_mute_self", error["error"]);
    }

    [Fact]
    public async Task MuteUser_AlreadyMuted_ShouldReturnBadRequest()
    {
        // Arrange
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ChatDbContext>();

        var channelName = Guid.NewGuid().ToString();
        var creator = new ReservedUsername
        {
            Id = Guid.NewGuid(),
            Username = "creator",
            Email = "creator@test.com",
            Provider = ExternalAuthProvider.Google,
            ExternalUserId = "ext-creator",
            CreatedAt = DateTime.UtcNow,
            LastLoginAt = DateTime.UtcNow
        };

        var targetUser = new ReservedUsername
        {
            Id = Guid.NewGuid(),
            Username = "target",
            Email = "target@test.com",
            Provider = ExternalAuthProvider.Google,
            ExternalUserId = "ext-target",
            CreatedAt = DateTime.UtcNow,
            LastLoginAt = DateTime.UtcNow
        };

        db.ReservedUsernames.AddRange(creator, targetUser);

        var channel = new Channel
        {
            Id = Guid.NewGuid(),
            Name = channelName,
            CreatedBy = creator.Username,
            CreatedAt = DateTime.UtcNow
        };
        db.Channels.Add(channel);

        var existingMute = new MutedUser
        {
            Id = Guid.NewGuid(),
            ChannelName = channel.Name,
            UserId = targetUser.Id.ToString(),
            MutedByUserId = creator.Id.ToString(),
            MutedAt = DateTime.UtcNow
        };
        db.MutedUsers.Add(existingMute);
        await db.SaveChangesAsync();

        var token = GenerateToken(creator);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var request = new { Reason = "Test" };

        // Act
        var response = await _client.PostAsJsonAsync(
            $"/api/channels/{channelName}/muted-users/{targetUser.Id}",
            request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();
        Assert.NotNull(error);
        Assert.Equal("user_already_muted", error["error"]);
    }

    [Fact]
    public async Task UnmuteUser_AsChannelCreator_ShouldUnmuteUser()
    {
        // Arrange
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ChatDbContext>();

        var creator = new ReservedUsername
        {
            Id = Guid.NewGuid(),
            Username = "creator",
            Email = "creator@test.com",
            Provider = ExternalAuthProvider.Google,
            ExternalUserId = "ext-creator",
            CreatedAt = DateTime.UtcNow,
            LastLoginAt = DateTime.UtcNow
        };

        var targetUser = new ReservedUsername
        {
            Id = Guid.NewGuid(),
            Username = "target",
            Email = "target@test.com",
            Provider = ExternalAuthProvider.Google,
            ExternalUserId = "ext-target",
            CreatedAt = DateTime.UtcNow,
            LastLoginAt = DateTime.UtcNow
        };

        db.ReservedUsernames.AddRange(creator, targetUser);

        var channel = new Channel
        {
            Id = Guid.NewGuid(),
            Name = Guid.NewGuid().ToString(),
            CreatedBy = creator.Username,
            CreatedAt = DateTime.UtcNow
        };
        db.Channels.Add(channel);

        var mute = new MutedUser
        {
            Id = Guid.NewGuid(),
            ChannelName = channel.Name,
            UserId = targetUser.Id.ToString(),
            MutedByUserId = creator.Id.ToString(),
            MutedAt = DateTime.UtcNow
        };
        db.MutedUsers.Add(mute);
        await db.SaveChangesAsync();

        var token = GenerateToken(creator);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.DeleteAsync(
            $"/api/channels/{channel.Name}/muted-users/{targetUser.Id}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // Verify removed from database
        using var verifyScope = factory.Services.CreateScope();
        using var verifyContext = verifyScope.ServiceProvider.GetRequiredService<ChatDbContext>();

        var removedMute = await verifyContext.MutedUsers
            .FirstOrDefaultAsync(m => m.UserId == targetUser.Id.ToString());

        Assert.Null(removedMute);
    }

    [Fact]
    public async Task UnmuteUser_CannotUnmuteSelf_ShouldReturnBadRequest()
    {
        // Arrange
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ChatDbContext>();

        var admin = new ReservedUsername
        {
            Id = Guid.NewGuid(),
            Username = "admin",
            Email = "admin@test.com",
            Provider = ExternalAuthProvider.Google,
            ExternalUserId = "ext-admin",
            CreatedAt = DateTime.UtcNow,
            LastLoginAt = DateTime.UtcNow,
            IsAdmin = true
        };

        var creator = new ReservedUsername
        {
            Id = Guid.NewGuid(),
            Username = "creator",
            Email = "creator@test.com",
            Provider = ExternalAuthProvider.Google,
            ExternalUserId = "ext-creator",
            CreatedAt = DateTime.UtcNow,
            LastLoginAt = DateTime.UtcNow
        };

        db.ReservedUsernames.AddRange(admin, creator);

        var channel = new Channel
        {
            Id = Guid.NewGuid(),
            Name = Guid.NewGuid().ToString(),
            CreatedBy = creator.Username,
            CreatedAt = DateTime.UtcNow
        };
        db.Channels.Add(channel);

        var mute = new MutedUser
        {
            Id = Guid.NewGuid(),
            ChannelName = channel.Name,
            UserId = admin.Id.ToString(),
            MutedByUserId = creator.Id.ToString(),
            MutedAt = DateTime.UtcNow
        };
        db.MutedUsers.Add(mute);
        await db.SaveChangesAsync();

        var token = GenerateToken(admin);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.DeleteAsync(
            $"/api/channels/{channel.Name}/muted-users/{admin.Id}");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();
        Assert.NotNull(error);
        Assert.Equal("cannot_unmute_self", error["error"]);
    }

    [Fact]
    public async Task UnmuteUser_UserNotMuted_ShouldReturnNotFound()
    {
        // Arrange
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ChatDbContext>();

        var creator = new ReservedUsername
        {
            Id = Guid.NewGuid(),
            Username = "creator",
            Email = "creator@test.com",
            Provider = ExternalAuthProvider.Google,
            ExternalUserId = "ext-creator",
            CreatedAt = DateTime.UtcNow,
            LastLoginAt = DateTime.UtcNow
        };

        db.ReservedUsernames.Add(creator);

        var channel = new Channel
        {
            Id = Guid.NewGuid(),
            Name = Guid.NewGuid().ToString(),
            CreatedBy = creator.Username,
            CreatedAt = DateTime.UtcNow
        };
        db.Channels.Add(channel);
        await db.SaveChangesAsync();

        var token = GenerateToken(creator);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.DeleteAsync(
            "/api/channels/general/muted-users/nonexistent-user");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task IsUserMuted_WithMutedUser_ShouldReturnTrue()
    {
        // Arrange
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ChatDbContext>();

        var user = new ReservedUsername
        {
            Id = Guid.NewGuid(),
            Username = "user",
            Email = "user@test.com",
            Provider = ExternalAuthProvider.Google,
            ExternalUserId = "ext-user",
            CreatedAt = DateTime.UtcNow,
            LastLoginAt = DateTime.UtcNow
        };

        db.ReservedUsernames.Add(user);

        var channel = new Channel
        {
            Id = Guid.NewGuid(),
            Name = Guid.NewGuid().ToString(),
            CreatedBy = "admin",
            CreatedAt = DateTime.UtcNow
        };
        db.Channels.Add(channel);

        var mute = new MutedUser
        {
            Id = Guid.NewGuid(),
            ChannelName = channel.Name,
            UserId = user.Id.ToString(),
            MutedByUserId = "admin-id",
            MutedAt = DateTime.UtcNow
        };
        db.MutedUsers.Add(mute);
        await db.SaveChangesAsync();

        // Act
        var response = await _client.GetAsync(
            $"/api/channels/{channel.Name}/muted-users/{user.Id}/is-muted");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        Assert.NotNull(result);
        Assert.True(((JsonElement)result["isMuted"]).GetBoolean());
    }

    [Fact]
    public async Task IsUserMuted_WithNonMutedUser_ShouldReturnFalse()
    {
        // Arrange
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ChatDbContext>();

        var channel = new Channel
        {
            Id = Guid.NewGuid(),
            Name = Guid.NewGuid().ToString(),
            CreatedBy = "admin",
            CreatedAt = DateTime.UtcNow
        };
        db.Channels.Add(channel);
        await db.SaveChangesAsync();

        // Act
        var response = await _client.GetAsync(
            "/api/channels/general/muted-users/user-123/is-muted");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        Assert.NotNull(result);
        Assert.False(((JsonElement)result["isMuted"]).GetBoolean());
    }

    [SuppressMessage("Blocker Vulnerability", "S6781:JWT secret keys should not be disclosed", Justification = "Test")]
    private static string GenerateToken(ReservedUsername user)
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
}