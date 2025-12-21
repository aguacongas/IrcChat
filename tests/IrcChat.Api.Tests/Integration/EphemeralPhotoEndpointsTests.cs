using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using IrcChat.Api.Data;
using IrcChat.Api.Services;
using IrcChat.Shared.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Moq;
using Xunit;

namespace IrcChat.Api.Tests.Integration;

public class EphemeralPhotoEndpointsTests(ApiWebApplicationFactory factory) : IClassFixture<ApiWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient();
    private readonly Mock<ICloudinaryService> _cloudinaryMock = factory.Services.GetRequiredService<Mock<ICloudinaryService>>();
    private readonly Mock<IEphemeralPhotoService> _ephemeralPhotoServiceMock = factory.Services.GetRequiredService<Mock<IEphemeralPhotoService>>();

    [Fact]
    public async Task UploadEphemeralPhoto_WithValidImage_ShouldReturnOk()
    {
        // Arrange
        var user = CreateTestUser();
        await SeedUser(user);

        var token = GenerateToken(user);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        _client.DefaultRequestHeaders.Add("x-ConnectionId", "test-connection-id");

        // Mock Cloudinary upload
        _cloudinaryMock
            .Setup(x => x.UploadEphemeralPhotoAsync(It.IsAny<byte[]>(), user.Id.ToString()))
            .ReturnsAsync(("https://cloudinary.com/image.jpg", "https://cloudinary.com/thumb.jpg"));

        // Créer une petite image 1x1 en base64
        var imageBase64 = Convert.ToBase64String(CreateTestImageBytes());
        var request = new UploadEphemeralPhotoRequest
        {
            ImageBase64 = imageBase64
        };

        // Act
        var response = await _client.PostAsJsonAsync($"/api/ephemeral-photos/{user.Id}/upload", request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<UploadEphemeralPhotoResponse>();
        Assert.NotNull(result);
        Assert.Equal("https://cloudinary.com/image.jpg", result.ImageUrl);
        Assert.Equal("https://cloudinary.com/thumb.jpg", result.ThumbnailUrl);

        _cloudinaryMock.Verify(
            x => x.UploadEphemeralPhotoAsync(It.IsAny<byte[]>(), user.Id.ToString()),
            Times.Once);
    }

    [Fact]
    public async Task UploadEphemeralPhoto_WithoutAuth_ShouldReturnUnauthorized()
    {
        // Arrange
        var imageBase64 = Convert.ToBase64String(CreateTestImageBytes());
        var request = new UploadEphemeralPhotoRequest
        {
            ImageBase64 = imageBase64
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/ephemeral-photos/test-user/upload", request);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task UploadEphemeralPhoto_WithWrongUserId_ShouldReturnForbidden()
    {
        // Arrange
        var user = CreateTestUser();
        await SeedUser(user);

        var token = GenerateToken(user);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        _client.DefaultRequestHeaders.Add("x-ConnectionId", "test-connection-id");

        var imageBase64 = Convert.ToBase64String(CreateTestImageBytes());
        var request = new UploadEphemeralPhotoRequest
        {
            ImageBase64 = imageBase64
        };

        // Act - Essayer d'uploader pour un autre utilisateur
        var response = await _client.PostAsJsonAsync("/api/ephemeral-photos/wrong-user-id/upload", request);

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task UploadEphemeralPhoto_WithInvalidBase64_ShouldReturnBadRequest()
    {
        // Arrange
        var user = CreateTestUser();
        await SeedUser(user);

        var token = GenerateToken(user);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        _client.DefaultRequestHeaders.Add("x-ConnectionId", "test-connection-id");

        _ephemeralPhotoServiceMock.Setup(x => x.CheckRateLimit(It.IsAny<string>()))
            .Returns(true);

        var request = new UploadEphemeralPhotoRequest
        {
            ImageBase64 = "invalid-base64!!!"
        };

        // Act
        var response = await _client.PostAsJsonAsync($"/api/ephemeral-photos/{user.Id}/upload", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UploadEphemeralPhoto_RateLimitExceeded_ShouldReturn429()
    {
        // Arrange
        var user = CreateTestUser();
        await SeedUser(user);

        var token = GenerateToken(user);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        _client.DefaultRequestHeaders.Add("x-ConnectionId", "test-connection-id");

        _cloudinaryMock
            .Setup(x => x.UploadEphemeralPhotoAsync(It.IsAny<byte[]>(), user.Id.ToString()))
            .ReturnsAsync(("https://cloudinary.com/image.jpg", "https://cloudinary.com/thumb.jpg"));

        _ephemeralPhotoServiceMock.Setup(x => x.CheckRateLimit(It.IsAny<string>()))
            .Returns(false);

        var imageBase64 = Convert.ToBase64String(CreateTestImageBytes());
        var request = new UploadEphemeralPhotoRequest
        {
            ImageBase64 = imageBase64
        };

        var response = await _client.PostAsJsonAsync($"/api/ephemeral-photos/{user.Id}/upload", request);
        Assert.Equal((HttpStatusCode)429, response.StatusCode);
    }

    [Fact]
    public async Task UploadEphemeralPhoto_CloudinaryError_ShouldReturn500()
    {
        // Arrange
        var user = CreateTestUser();
        await SeedUser(user);

        var token = GenerateToken(user);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        _client.DefaultRequestHeaders.Add("x-ConnectionId", "test-connection-id");

        // Mock Cloudinary error
        _cloudinaryMock
            .Setup(x => x.UploadEphemeralPhotoAsync(It.IsAny<byte[]>(), user.Id.ToString()))
            .ThrowsAsync(new InvalidOperationException("Cloudinary error"));

        _ephemeralPhotoServiceMock.Setup(x => x.ValidateImageAsync(It.IsAny<byte[]>(), It.IsAny<int>()))
            .ReturnsAsync(true);
        _ephemeralPhotoServiceMock.Setup(x => x.CheckRateLimit(It.IsAny<string>()))
            .Returns(true);

        var imageBase64 = Convert.ToBase64String(CreateTestImageBytes());
        var request = new UploadEphemeralPhotoRequest
        {
            ImageBase64 = imageBase64
        };

        // Act
        var response = await _client.PostAsJsonAsync($"/api/ephemeral-photos/{user.Id}/upload", request);

        // Assert
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
    }

    // ========== Helpers ==========

    private static ReservedUsername CreateTestUser(string? username = null)
    {
        var id = Guid.NewGuid();
        return new ReservedUsername
        {
            Id = id,
            Username = username ?? "testuser",
            Provider = ExternalAuthProvider.Google,
            ExternalUserId = id.ToString(),
            Email = "test@example.com",
            CreatedAt = DateTime.UtcNow,
            LastLoginAt = DateTime.UtcNow,
            IsAdmin = false
        };
    }

    private async Task SeedUser(ReservedUsername user)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ChatDbContext>();

        db.ReservedUsernames.Add(user);
        await db.SaveChangesAsync();
    }

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

    private static byte[] CreateTestImageBytes()
    {
        // Créer une petite image PNG 1x1 valide
        return
        [
            0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, // PNG signature
            0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52, // IHDR chunk
            0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01, // 1x1 dimensions
            0x08, 0x02, 0x00, 0x00, 0x00, 0x90, 0x77, 0x53,
            0xDE, 0x00, 0x00, 0x00, 0x0C, 0x49, 0x44, 0x41,
            0x54, 0x08, 0xD7, 0x63, 0xF8, 0xCF, 0xC0, 0x00,
            0x00, 0x03, 0x01, 0x01, 0x00, 0x18, 0xDD, 0x8D,
            0xB4, 0x00, 0x00, 0x00, 0x00, 0x49, 0x45, 0x4E,
            0x44, 0xAE, 0x42, 0x60, 0x82
        ];
    }
}