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

public class GlobalMuteEndpointsTests(ApiWebApplicationFactory factory) :
    IClassFixture<ApiWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task MuteUserGlobally_WithoutAuthentication_ShouldReturnUnauthorized()
    {
        // Act
        var response = await _client.PostAsJsonAsync(
            "/api/admin/global-mute/user-123",
            new { Reason = "Test" });

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task MuteUserGlobally_AsNonAdmin_ShouldReturnForbidden()
    {
        // Arrange
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ChatDbContext>();

        var nonAdminUser = new ReservedUsername
        {
            Id = Guid.NewGuid(),
            Username = "nonadmin",
            Email = "nonadmin@test.com",
            Provider = ExternalAuthProvider.Google,
            ExternalUserId = "ext-nonadmin",
            CreatedAt = DateTime.UtcNow,
            LastLoginAt = DateTime.UtcNow,
            IsAdmin = false
        };

        db.ReservedUsernames.Add(nonAdminUser);
        await db.SaveChangesAsync();

        var token = GenerateToken(nonAdminUser);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.PostAsJsonAsync(
            "/api/admin/global-mute/user-123",
            new { Reason = "Test" });

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task MuteUserGlobally_AsAdmin_ShouldMuteUser()
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

        db.ReservedUsernames.AddRange(admin, targetUser);
        await db.SaveChangesAsync();

        var token = GenerateToken(admin);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var request = new { Reason = "Spam" };

        // Act
        var response = await _client.PostAsJsonAsync(
            $"/api/admin/global-mute/{targetUser.Id}",
            request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var verifyScope = factory.Services.CreateScope();
        using var verifyContext = verifyScope.ServiceProvider.GetRequiredService<ChatDbContext>();

        var mute = await verifyContext.MutedUsers
            .FirstOrDefaultAsync(m => m.ChannelName == null && m.UserId == targetUser.Id.ToString());

        Assert.NotNull(mute);
        Assert.Null(mute!.ChannelName);
        Assert.Equal("Spam", mute.Reason);
        Assert.Equal(admin.Id.ToString(), mute.MutedByUserId);
    }

    [Fact]
    public async Task MuteUserGlobally_CannotMuteSelf_ShouldReturnBadRequest()
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

        db.ReservedUsernames.Add(admin);
        await db.SaveChangesAsync();

        var token = GenerateToken(admin);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var request = new { Reason = "Test" };

        // Act
        var response = await _client.PostAsJsonAsync(
            $"/api/admin/global-mute/{admin.Id}",
            request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();
        Assert.NotNull(error);
        Assert.Equal("cannot_mute_self", error["error"]);
    }

    [Fact]
    public async Task MuteUserGlobally_UserNotFound_ShouldReturnNotFound()
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

        db.ReservedUsernames.Add(admin);
        await db.SaveChangesAsync();

        var token = GenerateToken(admin);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var request = new { Reason = "Test" };

        // Act
        var response = await _client.PostAsJsonAsync(
            "/api/admin/global-mute/nonexistent-id",
            request);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();
        Assert.NotNull(error);
        Assert.Equal("user_not_found", error["error"]);
    }

    [Fact]
    public async Task MuteUserGlobally_AlreadyGloballyMuted_ShouldReturnBadRequest()
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

        db.ReservedUsernames.AddRange(admin, targetUser);

        var globalMute = new MutedUser
        {
            Id = Guid.NewGuid(),
            ChannelName = null,
            UserId = targetUser.Id.ToString(),
            MutedByUserId = admin.Id.ToString(),
            MutedAt = DateTime.UtcNow
        };
        db.MutedUsers.Add(globalMute);
        await db.SaveChangesAsync();

        var token = GenerateToken(admin);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var request = new { Reason = "Already muted" };

        // Act
        var response = await _client.PostAsJsonAsync(
            $"/api/admin/global-mute/{targetUser.Id}",
            request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();
        Assert.NotNull(error);
        Assert.Equal("user_already_globally_muted", error["error"]);
    }

    [Fact]
    public async Task UnmuteUserGlobally_AsAdmin_ShouldUnmuteUser()
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

        db.ReservedUsernames.AddRange(admin, targetUser);

        var globalMute = new MutedUser
        {
            Id = Guid.NewGuid(),
            ChannelName = null,
            UserId = targetUser.Id.ToString(),
            MutedByUserId = admin.Id.ToString(),
            MutedAt = DateTime.UtcNow
        };
        db.MutedUsers.Add(globalMute);
        await db.SaveChangesAsync();

        var token = GenerateToken(admin);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.DeleteAsync(
            $"/api/admin/global-mute/{targetUser.Id}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var verifyScope = factory.Services.CreateScope();
        using var verifyContext = verifyScope.ServiceProvider.GetRequiredService<ChatDbContext>();

        var removedMute = await verifyContext.MutedUsers
            .FirstOrDefaultAsync(m => m.ChannelName == null && m.UserId == targetUser.Id.ToString());

        Assert.Null(removedMute);
    }

    [Fact]
    public async Task UnmuteUserGlobally_UserNotGloballyMuted_ShouldReturnNotFound()
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

        db.ReservedUsernames.Add(admin);
        await db.SaveChangesAsync();

        var token = GenerateToken(admin);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.DeleteAsync(
            "/api/admin/global-mute/nonexistent-user");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();
        Assert.NotNull(error);
        Assert.Equal("user_not_globally_muted", error["error"]);
    }

    [Fact]
    public async Task IsUserGloballyMuted_WithGloballyMutedUser_ShouldReturnTrue()
    {
        // Arrange
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ChatDbContext>();

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

        db.ReservedUsernames.Add(targetUser);

        var globalMute = new MutedUser
        {
            Id = Guid.NewGuid(),
            ChannelName = null,
            UserId = targetUser.Id.ToString(),
            MutedByUserId = Guid.NewGuid().ToString(),
            MutedAt = DateTime.UtcNow
        };
        db.MutedUsers.Add(globalMute);

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

        db.ReservedUsernames.Add(admin);
        await db.SaveChangesAsync();

        var token = GenerateToken(admin);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.GetAsync(
            $"/api/admin/global-mute/{targetUser.Id}/is-muted");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        Assert.NotNull(result);
        Assert.True(((JsonElement)result["isGloballyMuted"]).GetBoolean());
    }

    [Fact]
    public async Task IsUserGloballyMuted_WithNonGloballyMutedUser_ShouldReturnFalse()
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

        db.ReservedUsernames.Add(admin);
        await db.SaveChangesAsync();

        var token = GenerateToken(admin);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.GetAsync(
            $"/api/admin/global-mute/{Guid.NewGuid()}/is-muted");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        Assert.NotNull(result);
        Assert.False(((JsonElement)result["isGloballyMuted"]).GetBoolean());
    }

    [Fact]
    public async Task GetGloballyMutedUsers_WithoutAuthentication_ShouldReturnUnauthorized()
    {
        // Act
        var response = await _client.GetAsync("/api/admin/global-mute");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetGloballyMutedUsers_AsNonAdmin_ShouldReturnForbidden()
    {
        // Arrange
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ChatDbContext>();

        var nonAdminUser = new ReservedUsername
        {
            Id = Guid.NewGuid(),
            Username = "nonadmin",
            Email = "nonadmin@test.com",
            Provider = ExternalAuthProvider.Google,
            ExternalUserId = "ext-nonadmin",
            CreatedAt = DateTime.UtcNow,
            LastLoginAt = DateTime.UtcNow,
            IsAdmin = false
        };

        db.ReservedUsernames.Add(nonAdminUser);
        await db.SaveChangesAsync();

        var token = GenerateToken(nonAdminUser);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.GetAsync("/api/admin/global-mute");

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetGloballyMutedUsers_AsAdmin_ShouldReturnList()
    {
        // Arrange
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ChatDbContext>();

        db.MutedUsers.RemoveRange(db.MutedUsers);
        await db.SaveChangesAsync();

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

        var mutedUser1 = new ReservedUsername
        {
            Id = Guid.NewGuid(),
            Username = "muted1",
            Email = "muted1@test.com",
            Provider = ExternalAuthProvider.Google,
            ExternalUserId = "ext-muted1",
            CreatedAt = DateTime.UtcNow,
            LastLoginAt = DateTime.UtcNow
        };

        var mutedUser2 = new ReservedUsername
        {
            Id = Guid.NewGuid(),
            Username = "muted2",
            Email = "muted2@test.com",
            Provider = ExternalAuthProvider.Google,
            ExternalUserId = "ext-muted2",
            CreatedAt = DateTime.UtcNow,
            LastLoginAt = DateTime.UtcNow
        };

        db.ReservedUsernames.AddRange(admin, mutedUser1, mutedUser2);

        var mute1 = new MutedUser
        {
            Id = Guid.NewGuid(),
            ChannelName = null,
            UserId = mutedUser1.Id.ToString(),
            MutedByUserId = admin.Id.ToString(),
            MutedAt = DateTime.UtcNow,
            Reason = "Spam"
        };

        var mute2 = new MutedUser
        {
            Id = Guid.NewGuid(),
            ChannelName = null,
            UserId = mutedUser2.Id.ToString(),
            MutedByUserId = admin.Id.ToString(),
            MutedAt = DateTime.UtcNow.AddMinutes(1),
            Reason = "Harassment"
        };

        db.MutedUsers.AddRange(mute1, mute2);
        await db.SaveChangesAsync();

        var token = GenerateToken(admin);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.GetAsync("/api/admin/global-mute");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<List<MutedUserResponse>>();
        Assert.NotNull(result);
        Assert.Equal(2, result.Count);
        Assert.Contains(result, r => r.UserId == mutedUser1.Id.ToString());
        Assert.Contains(result, r => r.UserId == mutedUser2.Id.ToString());
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