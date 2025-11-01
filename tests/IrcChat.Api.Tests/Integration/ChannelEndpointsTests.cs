// tests/IrcChat.Api.Tests/Integration/ChannelEndpointsTests.cs
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using FluentAssertions;
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
    private readonly ApiWebApplicationFactory _factory = factory;

    [Fact]
    public async Task GetChannels_ShouldReturnChannelList()
    {
        // Act
        var response = await _client.GetAsync("/api/channels");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var channels = await response.Content.ReadFromJsonAsync<List<Channel>>();
        channels.Should().NotBeNull();
    }

    [Fact]
    public async Task CreateChannel_WithAuthentication_ShouldCreateChannel()
    {
        // Arrange
        // D'abord, créer un utilisateur réservé avec OAuth dans la base de test
        using var scope = _factory.Services.CreateScope();
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

        // Générer un token JWT pour cet utilisateur OAuth
        var token = GenerateOAuthToken(reservedUser);
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        var channel = new Channel
        {
            Name = "test-channel",
            CreatedBy = reservedUser.Username
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/channels", channel);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await response.Content.ReadFromJsonAsync<Channel>();
        created.Should().NotBeNull();
        created!.Name.Should().Be("test-channel");
        created.CreatedBy.Should().Be(reservedUser.Username);
    }

    [Fact]
    public async Task CreateChannel_WithoutAuthentication_ShouldReturnForbidden()
    {
        // Arrange
        var channel = new Channel
        {
            Name = "test-channel",
            CreatedBy = "testuser"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/channels", channel);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task CreateChannel_DuplicateName_ShouldReturnBadRequest()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
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

        // Créer un premier canal
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
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

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
}