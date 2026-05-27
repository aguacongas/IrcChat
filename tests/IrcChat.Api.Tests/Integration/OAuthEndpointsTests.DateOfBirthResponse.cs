using System.Net;
using System.Net.Http.Json;
using IrcChat.Api.Data;
using IrcChat.Shared.Models;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace IrcChat.Api.Tests.Integration;

public partial class OAuthEndpointsTests
{
    [Fact]
    public async Task LoginReserved_ShouldReturnDateOfBirth()
    {
        // Arrange
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ChatDbContext>();

        db.ReservedUsernames.RemoveRange(db.ReservedUsernames);
        await db.SaveChangesAsync();

        var dateOfBirth = new DateTime(1990, 6, 15, 0, 0, 0, DateTimeKind.Utc);

        var user = new ReservedUsername
        {
            Id = Guid.NewGuid(),
            Username = "dob_login_user",
            Provider = ExternalAuthProvider.Google,
            ExternalUserId = "google-123",
            Email = "dob@example.com",
            CreatedAt = DateTime.UtcNow,
            LastLoginAt = DateTime.UtcNow,
            IsAdmin = false,
            DateOfBirth = dateOfBirth,
        };
        db.ReservedUsernames.Add(user);
        await db.SaveChangesAsync();

        var request = new OAuthTokenRequest
        {
            Provider = ExternalAuthProvider.Google,
            Code = "valid_code",
            RedirectUri = "http://localhost/callback",
            CodeVerifier = "valid_verifier",
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/oauth/login-reserved", request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<OAuthLoginResponse>();
        Assert.NotNull(result);
        Assert.Equal(dateOfBirth, result!.DateOfBirth);
    }

    [Fact]
    public async Task LoginReserved_DateOfBirth_ShouldBeUtc()
    {
        // Arrange
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ChatDbContext>();

        db.ReservedUsernames.RemoveRange(db.ReservedUsernames);
        await db.SaveChangesAsync();

        var dateOfBirth = new DateTime(1985, 3, 22, 0, 0, 0, DateTimeKind.Utc);

        var user = new ReservedUsername
        {
            Id = Guid.NewGuid(),
            Username = "dob_utc_user",
            Provider = ExternalAuthProvider.Google,
            ExternalUserId = "google-123",
            Email = "dob_utc@example.com",
            CreatedAt = DateTime.UtcNow,
            LastLoginAt = DateTime.UtcNow,
            IsAdmin = false,
            DateOfBirth = dateOfBirth,
        };
        db.ReservedUsernames.Add(user);
        await db.SaveChangesAsync();

        var request = new OAuthTokenRequest
        {
            Provider = ExternalAuthProvider.Google,
            Code = "valid_code",
            RedirectUri = "http://localhost/callback",
            CodeVerifier = "valid_verifier",
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/oauth/login-reserved", request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<OAuthLoginResponse>();
        Assert.NotNull(result);
        Assert.Equal(DateTimeKind.Utc, result!.DateOfBirth.Kind);
    }

    [Fact]
    public async Task ReserveUsername_ShouldReturnDateOfBirthInResponse()
    {
        // Arrange
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ChatDbContext>();

        db.ReservedUsernames.RemoveRange(db.ReservedUsernames);
        await db.SaveChangesAsync();

        var dateOfBirth = new DateTime(2000, 9, 10, 0, 0, 0, DateTimeKind.Utc);
        var userId = Guid.NewGuid();

        var request = new ReserveUsernameRequest
        {
            Username = Guid.NewGuid().ToString(),
            Provider = ExternalAuthProvider.Google,
            Code = "valid_code",
            RedirectUri = "http://localhost/callback",
            CodeVerifier = "valid_verifier",
            UserId = userId,
            DateOfBirth = dateOfBirth,
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/oauth/reserve-username", request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<OAuthLoginResponse>();
        Assert.NotNull(result);
        Assert.Equal(dateOfBirth, result!.DateOfBirth);
    }
}