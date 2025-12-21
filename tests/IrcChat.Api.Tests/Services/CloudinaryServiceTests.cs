using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using IrcChat.Api.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace IrcChat.Api.Tests.Services;

public class CloudinaryServiceTests
{
    private readonly Mock<ICloudinaryWrapper> _cloudinaryWrapperMock;
    private readonly Mock<ILogger<CloudinaryService>> _loggerMock;
    private readonly IOptions<CloudinaryOptions> _options;
    private readonly CloudinaryService _service;

    public CloudinaryServiceTests()
    {
        // Mock Cloudinary
        _cloudinaryWrapperMock = new Mock<ICloudinaryWrapper>();

        _loggerMock = new Mock<ILogger<CloudinaryService>>();

        // Configuration de test
        var cloudinaryOptions = new CloudinaryOptions
        {
            CloudName = "test-cloud",
            ApiKey = "test-key",
            ApiSecret = "test-secret",
            EphemeralFolder = "test-folder",
            SignedUrlExpirationHours = 24
        };
        _options = Options.Create(cloudinaryOptions);

        _service = new CloudinaryService(
            _cloudinaryWrapperMock.Object,
            _options,
            _loggerMock.Object);
    }

    [Fact]
    public async Task UploadEphemeralPhotoAsync_WithValidImage_ShouldReturnUrls()
    {
        // Arrange
        var imageBytes = CreateTestImageBytes();
        var userId = "test-user-id";

        var uploadResult = new ImageUploadResult
        {
            StatusCode = System.Net.HttpStatusCode.OK,
            PublicId = "test-folder/test-user-id/test-guid",
            SecureUrl = new Uri("https://res.cloudinary.com/test-cloud/image/upload/test.jpg")
        };

        _cloudinaryWrapperMock
            .Setup(x => x.UploadAsync(It.IsAny<ImageUploadParams>()))
            .ReturnsAsync(uploadResult);

        _cloudinaryWrapperMock.SetupGet(x => x.UrlImgUp).Returns(new Url("test"));

        // Act
        var (imageUrl, thumbnailUrl) = await _service.UploadEphemeralPhotoAsync(imageBytes, userId);

        // Assert
        Assert.NotNull(imageUrl);
        Assert.NotNull(thumbnailUrl);
        Assert.Contains("cloudinary.com", imageUrl);
        Assert.Contains("cloudinary.com", thumbnailUrl);

        _cloudinaryWrapperMock.Verify(
            x => x.UploadAsync(
                It.Is<ImageUploadParams>(p =>
                    p.PublicId.Contains(userId))),
            Times.Once);
    }

    [Fact]
    public async Task UploadEphemeralPhotoAsync_WithCloudinaryError_ShouldThrow()
    {
        // Arrange
        var imageBytes = CreateTestImageBytes();
        var userId = "test-user-id";

        var uploadResult = new ImageUploadResult
        {
            StatusCode = System.Net.HttpStatusCode.BadRequest,
            Error = new Error { Message = "Upload failed" }
        };

        _cloudinaryWrapperMock
            .Setup(x => x.UploadAsync(It.IsAny<ImageUploadParams>()))
            .ReturnsAsync(uploadResult);

        _cloudinaryWrapperMock.SetupGet(x => x.UrlImgUp).Returns(new Url("test"));

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await _service.UploadEphemeralPhotoAsync(imageBytes, userId));
    }

    [Fact]
    public async Task DeleteImageAsync_WithValidPublicId_ShouldReturnTrue()
    {
        // Arrange
        var publicId = "test-folder/test-user/test-image";

        var deleteResult = new DeletionResult
        {
            Result = "ok"
        };

        _cloudinaryWrapperMock
            .Setup(x => x.DestroyAsync(It.Is<DeletionParams>(p => p.PublicId == publicId)))
            .ReturnsAsync(deleteResult);

        // Act
        var result = await _service.DeleteImageAsync(publicId);

        // Assert
        Assert.True(result);

        _cloudinaryWrapperMock.Verify(
            x => x.DestroyAsync(It.Is<DeletionParams>(p => p.PublicId == publicId)),
            Times.Once);
    }

    [Fact]
    public async Task DeleteImageAsync_WithCloudinaryError_ShouldReturnFalse()
    {
        // Arrange
        var publicId = "test-folder/test-user/test-image";

        var deleteResult = new DeletionResult
        {
            Result = "not found"
        };

        _cloudinaryWrapperMock
            .Setup(x => x.DestroyAsync(It.Is<DeletionParams>(p => p.PublicId == publicId)))
            .ReturnsAsync(deleteResult);

        // Act
        var result = await _service.DeleteImageAsync(publicId);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task DeleteImageAsync_WithException_ShouldReturnFalse()
    {
        // Arrange
        var publicId = "test-folder/test-user/test-image";

        _cloudinaryWrapperMock
            .Setup(x => x.DestroyAsync(It.IsAny<DeletionParams>()))
            .ThrowsAsync(new Exception("Cloudinary error"));

        // Act
        var result = await _service.DeleteImageAsync(publicId);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task UploadEphemeralPhotoAsync_ShouldGenerateThumbnailWithBlur()
    {
        // Arrange
        var imageBytes = CreateTestImageBytes();
        var userId = "test-user-id";

        var uploadResult = new ImageUploadResult
        {
            StatusCode = System.Net.HttpStatusCode.OK,
            PublicId = "test-folder/test-user-id/test-guid",
            SecureUrl = new Uri("https://res.cloudinary.com/test-cloud/image/upload/test.jpg")
        };

        _cloudinaryWrapperMock
            .Setup(x => x.UploadAsync(It.IsAny<ImageUploadParams>()))
            .ReturnsAsync(uploadResult);

        _cloudinaryWrapperMock.SetupGet(x => x.UrlImgUp).Returns(new Url("test"));
        // Act
        var (imageUrl, thumbnailUrl) = await _service.UploadEphemeralPhotoAsync(imageBytes, userId);

        // Assert
        // La thumbnail devrait contenir des transformations (blur, crop)
        Assert.NotEqual(imageUrl, thumbnailUrl);

        // On ne peut pas vérifier le contenu exact de l'URL car elle est générée par Cloudinary SDK
        // mais on vérifie qu'elle existe et est différente de l'image principale
        Assert.NotNull(thumbnailUrl);
        Assert.NotEmpty(thumbnailUrl);
    }

    [Fact]
    public void CloudinaryOptions_ShouldHaveCorrectDefaults()
    {
        // Arrange & Act
        var options = new CloudinaryOptions
        {
            CloudName = "test",
            ApiKey = "key",
            ApiSecret = "secret"
        };

        // Assert
        Assert.Equal("ircchat-ephemeral", options.EphemeralFolder);
        Assert.Equal(24, options.SignedUrlExpirationHours);
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