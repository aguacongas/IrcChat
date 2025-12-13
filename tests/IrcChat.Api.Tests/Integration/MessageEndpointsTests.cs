using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using IrcChat.Api.Data;
using IrcChat.Shared.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace IrcChat.Api.Tests.Integration;

public class MessageEndpointsTests(ApiWebApplicationFactory factory)
    : IClassFixture<ApiWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task GetMessages_ShouldReturnNonDeletedMessagesForChannel()
    {
        // Arrange
        var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ChatDbContext>();

        var channel = Guid.NewGuid().ToString();
        var userId = Guid.NewGuid().ToString();

        db.Messages.Add(new Message
        {
            UserId = userId,
            Username = "testuser",
            Content = "Test message 1",
            Channel = channel,
            IsDeleted = true,
            Timestamp = DateTime.UtcNow,
        });
        db.Messages.Add(new Message
        {
            UserId = userId,
            Username = "testuser",
            Content = "Test message 2",
            Channel = channel,
            Timestamp = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        // Act
        var response = await _client.GetAsync($"/api/messages/{channel}?userId={userId}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var messages = await response.Content.ReadFromJsonAsync<List<Message>>();
        Assert.NotNull(messages);
        Assert.Single(messages);
    }

    [Fact]
    public async Task DeleteMessage_AsChannelCreator_ShouldDeleteMessage()
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
            IsAdmin = false,
        };
        db.ReservedUsernames.Add(creator);

        var channel = new Channel
        {
            Id = Guid.NewGuid(),
            Name = "test-channel",
            CreatedBy = creator.Username,
            CreatedAt = DateTime.UtcNow,
        };
        db.Channels.Add(channel);

        var message = new Message
        {
            Id = Guid.NewGuid(),
            UserId = "other-user",
            Username = "otheruser",
            Content = "Message to delete",
            Channel = channel.Name,
            Timestamp = DateTime.UtcNow,
            IsDeleted = false,
        };
        db.Messages.Add(message);
        await db.SaveChangesAsync();

        var token = GenerateToken(creator);
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.DeleteAsync($"/api/messages/{message.Channel}/{message.Id}");

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        using var verifyScope = factory.Services.CreateScope();
        using var verifyContext = verifyScope.ServiceProvider.GetRequiredService<ChatDbContext>();
        var deletedMessage = await verifyContext.Messages.FindAsync(message.Id);
        Assert.NotNull(deletedMessage);
        Assert.True(deletedMessage!.IsDeleted);
    }

    [Fact]
    public async Task DeleteMessage_AsAdmin_ShouldDeleteMessage()
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
            IsAdmin = true,
        };
        db.ReservedUsernames.Add(admin);

        var channel = new Channel
        {
            Id = Guid.NewGuid(),
            Name = "test-channel",
            CreatedBy = "creator",
            CreatedAt = DateTime.UtcNow,
        };
        db.Channels.Add(channel);

        var message = new Message
        {
            Id = Guid.NewGuid(),
            UserId = "user-id",
            Username = "someuser",
            Content = "Message to delete",
            Channel = channel.Name,
            Timestamp = DateTime.UtcNow,
            IsDeleted = false,
        };
        db.Messages.Add(message);
        await db.SaveChangesAsync();

        var token = GenerateToken(admin);
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.DeleteAsync($"/api/messages/{message.Channel}/{message.Id}");

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        using var verifyScope = factory.Services.CreateScope();
        using var verifyContext = verifyScope.ServiceProvider.GetRequiredService<ChatDbContext>();
        var deletedMessage = await verifyContext.Messages.FindAsync(message.Id);
        Assert.NotNull(deletedMessage);
        Assert.True(deletedMessage!.IsDeleted);
    }

    [Fact]
    public async Task DeleteMessage_AsRegularUser_ShouldReturnForbidden()
    {
        // Arrange
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ChatDbContext>();

        var regularUser = new ReservedUsername
        {
            Id = Guid.NewGuid(),
            Username = "regularuser",
            Email = "regular@test.com",
            Provider = ExternalAuthProvider.Google,
            ExternalUserId = "ext-regular",
            CreatedAt = DateTime.UtcNow,
            LastLoginAt = DateTime.UtcNow,
            IsAdmin = false,
        };
        db.ReservedUsernames.Add(regularUser);

        var channel = new Channel
        {
            Id = Guid.NewGuid(),
            Name = "test-channel",
            CreatedBy = "creator",
            CreatedAt = DateTime.UtcNow,
        };
        db.Channels.Add(channel);

        var message = new Message
        {
            Id = Guid.NewGuid(),
            UserId = "other-user",
            Username = "otheruser",
            Content = "Protected message",
            Channel = channel.Name,
            Timestamp = DateTime.UtcNow,
            IsDeleted = false,
        };
        db.Messages.Add(message);
        await db.SaveChangesAsync();

        var token = GenerateToken(regularUser);
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.DeleteAsync($"/api/messages/{message.Channel}/{message.Id}");

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        using var verifyScope = factory.Services.CreateScope();
        using var verifyContext = verifyScope.ServiceProvider.GetRequiredService<ChatDbContext>();
        var notDeletedMessage = await verifyContext.Messages.FindAsync(message.Id);
        Assert.NotNull(notDeletedMessage);
        Assert.False(notDeletedMessage!.IsDeleted);
    }

    [Fact]
    public async Task DeleteMessage_WithoutAuthentication_ShouldReturnUnauthorized()
    {
        // Arrange
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ChatDbContext>();

        var message = new Message
        {
            Id = Guid.NewGuid(),
            UserId = "user-id",
            Username = "someuser",
            Content = "Message",
            Channel = "general",
            Timestamp = DateTime.UtcNow,
            IsDeleted = false,
        };
        db.Messages.Add(message);
        await db.SaveChangesAsync();

        // Act
        var response = await _client.DeleteAsync($"/api/messages/{message.Channel}/{message.Id}");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task DeleteMessage_WithNonExistentMessage_ShouldReturnNotFound()
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
            IsAdmin = true,
        };
        db.ReservedUsernames.Add(admin);
        var channel = new Channel
        {
            Id = Guid.NewGuid(),
            Name = "test-channel",
            CreatedBy = "creator",
            CreatedAt = DateTime.UtcNow,
        };
        db.Channels.Add(channel);

        await db.SaveChangesAsync();

        var token = GenerateToken(admin);
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var nonExistentId = Guid.NewGuid();

        // Act
        var response = await _client.DeleteAsync($"/api/messages/{channel.Name}/{nonExistentId}");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task DeleteMessage_AlreadyDeleted_ShouldStillReturnNoContent()
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
            IsAdmin = true,
        };
        db.ReservedUsernames.Add(admin);

        var channel = new Channel
        {
            Id = Guid.NewGuid(),
            Name = "test-channel",
            CreatedBy = "creator",
            CreatedAt = DateTime.UtcNow,
        };
        db.Channels.Add(channel);

        var message = new Message
        {
            Id = Guid.NewGuid(),
            UserId = "user-id",
            Username = "someuser",
            Content = "Already deleted message",
            Channel = channel.Name,
            Timestamp = DateTime.UtcNow,
            IsDeleted = true,
        };
        db.Messages.Add(message);
        await db.SaveChangesAsync();

        var token = GenerateToken(admin);
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.DeleteAsync($"/api/messages/{message.Channel}/{message.Id}");

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task DeleteMessage_CreatorCanDeleteAnyMessage_InTheirChannel()
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
            IsAdmin = false,
        };
        db.ReservedUsernames.Add(creator);

        var channel = new Channel
        {
            Id = Guid.NewGuid(),
            Name = "my-channel",
            CreatedBy = creator.Username,
            CreatedAt = DateTime.UtcNow,
        };
        db.Channels.Add(channel);

        var otherUserMessage = new Message
        {
            Id = Guid.NewGuid(),
            UserId = "other-user-id",
            Username = "otheruser",
            Content = "Message from another user",
            Channel = channel.Name,
            Timestamp = DateTime.UtcNow,
            IsDeleted = false,
        };
        db.Messages.Add(otherUserMessage);
        await db.SaveChangesAsync();

        var token = GenerateToken(creator);
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.DeleteAsync($"/api/messages/{otherUserMessage.Channel}/{otherUserMessage.Id}");

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        using var verifyScope = factory.Services.CreateScope();
        using var verifyContext = verifyScope.ServiceProvider.GetRequiredService<ChatDbContext>();
        var deletedMessage = await verifyContext.Messages.FindAsync(otherUserMessage.Id);
        Assert.NotNull(deletedMessage);
        Assert.True(deletedMessage!.IsDeleted);
    }

    [Fact]
    public async Task DeleteMessage_CreatorCannotDelete_InOtherChannel()
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
            IsAdmin = false,
        };
        db.ReservedUsernames.Add(creator);

        var myChannel = new Channel
        {
            Id = Guid.NewGuid(),
            Name = "my-channel",
            CreatedBy = creator.Username,
            CreatedAt = DateTime.UtcNow,
        };
        db.Channels.Add(myChannel);

        var otherChannel = new Channel
        {
            Id = Guid.NewGuid(),
            Name = "other-channel",
            CreatedBy = "othercreator",
            CreatedAt = DateTime.UtcNow,
        };
        db.Channels.Add(otherChannel);

        var message = new Message
        {
            Id = Guid.NewGuid(),
            UserId = "user-id",
            Username = "someuser",
            Content = "Message in other channel",
            Channel = otherChannel.Name,
            Timestamp = DateTime.UtcNow,
            IsDeleted = false,
        };
        db.Messages.Add(message);
        await db.SaveChangesAsync();

        var token = GenerateToken(creator);
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.DeleteAsync($"/api/messages/{message.Channel}/{message.Id}");

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        using var verifyScope = factory.Services.CreateScope();
        using var verifyContext = verifyScope.ServiceProvider.GetRequiredService<ChatDbContext>();
        var notDeletedMessage = await verifyContext.Messages.FindAsync(message.Id);
        Assert.NotNull(notDeletedMessage);
        Assert.False(notDeletedMessage!.IsDeleted);
    }

    private static string GenerateToken(ReservedUsername user)
    {
        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes("VotreCleSecrete123456789012345678901234567890"));

        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.Username),
            new(ClaimTypes.Email, user.Email),
            new("provider", user.Provider.ToString()),
        };

        if (user.IsAdmin)
        {
            claims.Add(new Claim(ClaimTypes.Role, "Admin"));
        }

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