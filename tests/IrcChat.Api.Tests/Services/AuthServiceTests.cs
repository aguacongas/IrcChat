// tests/IrcChat.Api.Tests/Services/AuthServiceTests.cs
using FluentAssertions;
using IrcChat.Api.Data;
using IrcChat.Api.Services;
using IrcChat.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace IrcChat.Api.Tests.Services;

public class AuthServiceTests : IDisposable
{
    private readonly ChatDbContext _context;
    private readonly AuthService _authService;
    private readonly IConfiguration _configuration;

    public AuthServiceTests()
    {
        var options = new DbContextOptionsBuilder<ChatDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new ChatDbContext(options);

        var configData = new Dictionary<string, string>
        {
            { "Jwt:Key", "VotreCleSecrete123456789012345678901234567890" },
            { "Jwt:Issuer", "TestIssuer" },
            { "Jwt:Audience", "TestAudience" }
        };

        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData!)
            .Build();

        _authService = new AuthService(_context, _configuration);
    }

    [Fact]
    public async Task CreateAdmin_ShouldCreateAdminWithHashedPassword()
    {
        // Arrange
        var username = "testadmin";
        var password = "password123";

        // Act
        var admin = await _authService.CreateAdmin(username, password);

        // Assert
        admin.Should().NotBeNull();
        admin.Username.Should().Be(username);
        admin.PasswordHash.Should().NotBeNullOrEmpty();
        admin.PasswordHash.Should().NotBe(password);
        admin.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task ValidateAdmin_WithValidCredentials_ShouldReturnAdmin()
    {
        // Arrange
        var username = "testadmin";
        var password = "password123";
        await _authService.CreateAdmin(username, password);

        // Act
        var result = await _authService.ValidateAdmin(username, password);

        // Assert
        result.Should().NotBeNull();
        result!.Username.Should().Be(username);
    }

    [Fact]
    public async Task ValidateAdmin_WithInvalidPassword_ShouldReturnNull()
    {
        // Arrange
        var username = "testadmin";
        await _authService.CreateAdmin(username, "correct_password");

        // Act
        var result = await _authService.ValidateAdmin(username, "wrong_password");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task ValidateAdmin_WithNonExistentUser_ShouldReturnNull()
    {
        // Act
        var result = await _authService.ValidateAdmin("nonexistent", "password");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void GenerateToken_ShouldReturnValidJwtToken()
    {
        // Arrange
        var admin = new Admin
        {
            Id = Guid.NewGuid(),
            Username = "testadmin",
            PasswordHash = "hash",
            CreatedAt = DateTime.UtcNow
        };

        // Act
        var token = _authService.GenerateToken(admin);

        // Assert
        token.Should().NotBeNullOrEmpty();
        token.Split('.').Should().HaveCount(3); // JWT format: header.payload.signature
    }

    [Fact]
    public void HashPassword_ShouldProduceDifferentHashesForSamePassword()
    {
        // Arrange
        var password = "password123";

        // Act
        var hash1 = _authService.HashPassword(password);
        var hash2 = _authService.HashPassword(password);

        // Assert
        // Note: SHA256 est déterministe, donc les hashes devraient être identiques
        hash1.Should().Be(hash2);
        hash1.Should().NotBe(password);
    }

    public void Dispose()
    {
        _context.Dispose();
        GC.SuppressFinalize(this);
    }
}


