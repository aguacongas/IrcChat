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
    public async Task ReserveUsername_WithValidAge_ShouldSucceed()
    {
        // Arrange
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ChatDbContext>();
        // S'assurer qu'il n'y a pas d'utilisateurs
        db.ReservedUsernames.RemoveRange(db.ReservedUsernames);
        await db.SaveChangesAsync();

        var dateOfBirth = DateTime.UtcNow.AddYears(-20);
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

        using var verifyScope = factory.Services.CreateScope();
        using var verifyContext = verifyScope.ServiceProvider.GetRequiredService<ChatDbContext>();

        var savedUser = await verifyContext.ReservedUsernames.FindAsync(userId);
        Assert.NotNull(savedUser);
        Assert.Equal(request.Username, savedUser.Username);
        Assert.Equal(dateOfBirth.Date, savedUser.DateOfBirth.Date);
    }

    [Fact]
    public async Task ReserveUsername_WithAge13_ShouldSucceed()
    {
        // Arrange
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ChatDbContext>();
        // S'assurer qu'il n'y a pas d'utilisateurs
        db.ReservedUsernames.RemoveRange(db.ReservedUsernames);
        await db.SaveChangesAsync();

        var dateOfBirth = DateTime.UtcNow.AddYears(-13);
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
    }

    [Fact]
    public async Task ReserveUsername_WithAge120_ShouldSucceed()
    {
        // Arrange
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ChatDbContext>();

        var dateOfBirth = DateTime.UtcNow.AddYears(-120);
        var userId = Guid.NewGuid();

        var request = new ReserveUsernameRequest
        {
            Username = "age120_user",
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
    }

    [Fact]
    public async Task ReserveUsername_WithAgeLessThan13_ShouldReturnBadRequest()
    {
        // Arrange
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ChatDbContext>();

        var dateOfBirth = DateTime.UtcNow.AddYears(-10);
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
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var errorResponse = await response.Content.ReadAsStringAsync();
        Assert.Contains("L'âge minimum requis est de 13 ans", errorResponse);

        using var verifyScope = factory.Services.CreateScope();
        using var verifyContext = verifyScope.ServiceProvider.GetRequiredService<ChatDbContext>();

        var savedUser = await verifyContext.ReservedUsernames.FindAsync(userId);
        Assert.Null(savedUser);
    }

    [Fact]
    public async Task ReserveUsername_WithAge12_ShouldReturnBadRequest()
    {
        // Arrange
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ChatDbContext>();

        var dateOfBirth = DateTime.UtcNow.AddYears(-12);
        var userId = Guid.NewGuid();

        var request = new ReserveUsernameRequest
        {
            Username = "age12_user",
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
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ReserveUsername_WithAgeGreaterThan120_ShouldReturnBadRequest()
    {
        // Arrange
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ChatDbContext>();

        var dateOfBirth = DateTime.UtcNow.AddYears(-130);
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
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var errorResponse = await response.Content.ReadAsStringAsync();
        Assert.Contains("L'âge ne peut pas dépasser 120 ans", errorResponse);

        using var verifyScope = factory.Services.CreateScope();
        using var verifyContext = verifyScope.ServiceProvider.GetRequiredService<ChatDbContext>();

        var savedUser = await verifyContext.ReservedUsernames.FindAsync(userId);
        Assert.Null(savedUser);
    }

    [Fact]
    public async Task ReserveUsername_WithAge121_ShouldReturnBadRequest()
    {
        // Arrange
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ChatDbContext>();

        var dateOfBirth = DateTime.UtcNow.AddYears(-121);
        var userId = Guid.NewGuid();

        var request = new ReserveUsernameRequest
        {
            Username = "age121_user",
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
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ReserveUsername_ShouldStoreDateOfBirthInUtc()
    {
        // Arrange
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ChatDbContext>();
        // S'assurer qu'il n'y a pas d'utilisateurs
        db.ReservedUsernames.RemoveRange(db.ReservedUsernames);
        await db.SaveChangesAsync();

        var dateOfBirth = new DateTime(2000, 6, 15, 0, 0, 0, DateTimeKind.Utc);
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

        using var verifyScope = factory.Services.CreateScope();
        using var verifyContext = verifyScope.ServiceProvider.GetRequiredService<ChatDbContext>();

        var savedUser = await verifyContext.ReservedUsernames.FindAsync(userId);
        Assert.NotNull(savedUser);
        Assert.Equal(DateTimeKind.Utc, savedUser.DateOfBirth.Kind);
        Assert.Equal(dateOfBirth, savedUser.DateOfBirth);
    }

    [Fact]
    public async Task ReserveUsername_WithEmptyUsername_ShouldReturnBadRequest()
    {
        // Arrange
        var dateOfBirth = DateTime.UtcNow.AddYears(-20);
        var userId = Guid.NewGuid();

        var request = new ReserveUsernameRequest
        {
            Username = string.Empty,
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
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errorResponse = await response.Content.ReadAsStringAsync();
        Assert.Contains("Le pseudo", errorResponse);
    }
}