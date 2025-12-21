using System.Reflection;
using IrcChat.Api.Services;
using Microsoft.Extensions.Logging;
using Moq;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using Xunit;

namespace IrcChat.Api.Tests.Services;

/// <summary>
/// Tests pour EphemeralPhotoService (Backend)
/// </summary>
public class EphemeralPhotoServiceTests : IDisposable
{
    private readonly Mock<ILogger<EphemeralPhotoService>> _loggerMock;
    private readonly EphemeralPhotoService _service;

    public EphemeralPhotoServiceTests()
    {
        _loggerMock = new Mock<ILogger<EphemeralPhotoService>>();
        _service = new EphemeralPhotoService(_loggerMock.Object);
    }

    public void Dispose()
    {
        // Cleanup du ConcurrentDictionary statique pour éviter les side-effects entre tests
        var rateLimitStore = typeof(EphemeralPhotoService)
            .GetField("_rateLimitStore", BindingFlags.NonPublic | BindingFlags.Static);

        if (rateLimitStore?.GetValue(null) is System.Collections.Concurrent.ConcurrentDictionary<string, Queue<DateTime>> store)
        {
            store.Clear();
        }

        GC.SuppressFinalize(this);
    }

    // ==================== Helpers ====================

    /// <summary>
    /// Génère une image de test en mémoire
    /// </summary>
    private static byte[] GenerateTestImage(int width, int height, ImageFormat format = ImageFormat.Jpeg)
    {
        using var image = new Image<Rgba32>(width, height);

        // Remplir avec un dégradé de couleurs pour avoir une vraie image
        image.Mutate(ctx =>
        {
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    var r = (byte)((x * 255) / width);
                    var g = (byte)((y * 255) / height);
                    var b = (byte)(128);
                    image[x, y] = new Rgba32(r, g, b);
                }
            }
        });

        using var ms = new MemoryStream();

        if (format == ImageFormat.Jpeg)
        {
            image.SaveAsJpeg(ms);
        }
        else if (format == ImageFormat.Png)
        {
            image.SaveAsPng(ms);
        }

        return ms.ToArray();
    }

    /// <summary>
    /// Génère des bytes corrompus (pas une vraie image)
    /// </summary>
    private static byte[] GenerateCorruptedImage() => "This is not a valid image"u8.ToArray();

    private enum ImageFormat
    {
        Jpeg,
        Png
    }

    // ==================== Tests ValidateImageAsync ====================

    [Fact]
    public async Task ValidateImageAsync_ValidJpegImage_ReturnsTrue()
    {
        // Arrange
        var imageBytes = GenerateTestImage(800, 600, ImageFormat.Jpeg);
        var maxSizeKb = 2048; // 2MB

        // Act
        var result = await _service.ValidateImageAsync(imageBytes, maxSizeKb);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task ValidateImageAsync_ValidPngImage_ReturnsTrue()
    {
        // Arrange
        var imageBytes = GenerateTestImage(1024, 768, ImageFormat.Png);
        var maxSizeKb = 2048;

        // Act
        var result = await _service.ValidateImageAsync(imageBytes, maxSizeKb);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task ValidateImageAsync_NullImage_ReturnsFalse()
    {
        // Arrange
        byte[]? imageBytes = null;
        var maxSizeKb = 2048;

        // Act
        var result = await _service.ValidateImageAsync(imageBytes!, maxSizeKb);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task ValidateImageAsync_ImageTooLarge_ReturnsFalse()
    {
        // Arrange
        var imageBytes = GenerateTestImage(2000, 2000, ImageFormat.Jpeg); // Grosse image
        var maxSizeKb = 10; // Limite très basse

        // Act
        var result = await _service.ValidateImageAsync(imageBytes, maxSizeKb);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task ValidateImageAsync_ImageAtExactLimit_ReturnsTrue()
    {
        // Arrange
        var imageBytes = GenerateTestImage(100, 100, ImageFormat.Jpeg);
        var imageSizeKb = imageBytes.Length / 1024;
        var maxSizeKb = imageSizeKb; // Exactement la limite

        // Act
        var result = await _service.ValidateImageAsync(imageBytes, maxSizeKb);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task ValidateImageAsync_CorruptedImage_ReturnsFalse()
    {
        // Arrange
        var imageBytes = GenerateCorruptedImage();
        var maxSizeKb = 2048;

        // Act
        var result = await _service.ValidateImageAsync(imageBytes, maxSizeKb);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task ValidateImageAsync_EmptyImage_ReturnsFalse()
    {
        // Arrange
        var imageBytes = Array.Empty<byte>();
        var maxSizeKb = 2048;

        // Act
        var result = await _service.ValidateImageAsync(imageBytes, maxSizeKb);

        // Assert
        Assert.False(result);
    }

    // ==================== Tests GenerateBlurredThumbnailAsync ====================

    [Fact]
    public async Task GenerateBlurredThumbnailAsync_ValidImage_ReturnsThumbnail()
    {
        // Arrange
        var imageBytes = GenerateTestImage(1920, 1080, ImageFormat.Jpeg);

        // Act
        var thumbnailBytes = await _service.GenerateBlurredThumbnailAsync(imageBytes);

        // Assert
        Assert.NotNull(thumbnailBytes);
        Assert.NotEmpty(thumbnailBytes);
        Assert.True(thumbnailBytes.Length < imageBytes.Length); // Thumbnail doit être plus petite
    }

    [Fact]
    public async Task GenerateBlurredThumbnailAsync_ValidImage_ThumbnailHasCorrectMaxSize()
    {
        // Arrange
        var imageBytes = GenerateTestImage(2000, 1500, ImageFormat.Jpeg);

        // Act
        var thumbnailBytes = await _service.GenerateBlurredThumbnailAsync(imageBytes);

        // Assert
        using var ms = new MemoryStream(thumbnailBytes);
        using var thumbnail = await Image.LoadAsync(ms);

        // Vérifier que la thumbnail respecte la taille max (200x200)
        Assert.True(thumbnail.Width <= 200);
        Assert.True(thumbnail.Height <= 200);
    }

    [Fact]
    public async Task GenerateBlurredThumbnailAsync_ValidImage_OutputIsJpeg()
    {
        // Arrange
        var imageBytes = GenerateTestImage(800, 600, ImageFormat.Png);

        // Act
        var thumbnailBytes = await _service.GenerateBlurredThumbnailAsync(imageBytes);

        // Assert
        using var ms = new MemoryStream(thumbnailBytes);
        var format = await Image.DetectFormatAsync(ms);

        Assert.NotNull(format);
        Assert.Equal("JPEG", format.Name);
    }

    [Fact]
    public async Task GenerateBlurredThumbnailAsync_SmallImage_StillGeneratesThumbnail()
    {
        // Arrange
        var imageBytes = GenerateTestImage(100, 100, ImageFormat.Jpeg);

        // Act
        var thumbnailBytes = await _service.GenerateBlurredThumbnailAsync(imageBytes);

        // Assert
        Assert.NotNull(thumbnailBytes);
        Assert.NotEmpty(thumbnailBytes);

        using var ms = new MemoryStream(thumbnailBytes);
        using var thumbnail = await Image.LoadAsync(ms);

        Assert.True(thumbnail.Width <= 200);
        Assert.True(thumbnail.Height <= 200);
    }

    [Fact]
    public async Task GenerateBlurredThumbnailAsync_CorruptedImage_ThrowsException()
    {
        // Arrange
        var imageBytes = GenerateCorruptedImage();

        // Act & Assert
        await Assert.ThrowsAsync<UnknownImageFormatException>(() =>
            _service.GenerateBlurredThumbnailAsync(imageBytes));
    }

    // ==================== Tests CompressImageAsync ====================

    [Fact]
    public async Task CompressImageAsync_ValidImage_ReturnsCompressedImage()
    {
        // Arrange
        var imageBytes = GenerateTestImage(1000, 800, ImageFormat.Jpeg);
        var quality = 75;

        // Act
        var compressedBytes = await _service.CompressImageAsync(imageBytes, quality);

        // Assert
        Assert.NotNull(compressedBytes);
        Assert.NotEmpty(compressedBytes);
    }

    [Fact]
    public async Task CompressImageAsync_LargeImage_IsResized()
    {
        // Arrange
        var imageBytes = GenerateTestImage(3000, 2500, ImageFormat.Jpeg); // Plus grand que 1920x1080
        var quality = 75;

        // Act
        var compressedBytes = await _service.CompressImageAsync(imageBytes, quality);

        // Assert
        using var ms = new MemoryStream(compressedBytes);
        using var image = await Image.LoadAsync(ms);

        // Vérifier que l'image a été redimensionnée
        Assert.True(image.Width <= 1920);
        Assert.True(image.Height <= 1080);
    }

    [Fact]
    public async Task CompressImageAsync_SmallImage_NotResized()
    {
        // Arrange
        var originalWidth = 800;
        var originalHeight = 600;
        var imageBytes = GenerateTestImage(originalWidth, originalHeight, ImageFormat.Jpeg);
        var quality = 75;

        // Act
        var compressedBytes = await _service.CompressImageAsync(imageBytes, quality);

        // Assert
        using var ms = new MemoryStream(compressedBytes);
        using var image = await Image.LoadAsync(ms);

        // Image ne devrait pas être redimensionnée (déjà < 1920x1080)
        Assert.Equal(originalWidth, image.Width);
        Assert.Equal(originalHeight, image.Height);
    }

    [Fact]
    public async Task CompressImageAsync_ValidImage_OutputIsJpeg()
    {
        // Arrange
        var imageBytes = GenerateTestImage(800, 600, ImageFormat.Png);
        var quality = 75;

        // Act
        var compressedBytes = await _service.CompressImageAsync(imageBytes, quality);

        // Assert
        using var ms = new MemoryStream(compressedBytes);
        var format = await Image.DetectFormatAsync(ms);

        Assert.NotNull(format);
        Assert.Equal("JPEG", format.Name);
    }

    [Fact]
    public async Task CompressImageAsync_HighQuality_LargerFileSize()
    {
        // Arrange
        var imageBytes = GenerateTestImage(1000, 800, ImageFormat.Jpeg);

        // Act
        var lowQualityBytes = await _service.CompressImageAsync(imageBytes, 50);
        var highQualityBytes = await _service.CompressImageAsync(imageBytes, 95);

        // Assert
        Assert.True(highQualityBytes.Length > lowQualityBytes.Length);
    }

    [Fact]
    public async Task CompressImageAsync_CorruptedImage_ThrowsException()
    {
        // Arrange
        var imageBytes = GenerateCorruptedImage();
        var quality = 75;

        // Act & Assert
        await Assert.ThrowsAsync<UnknownImageFormatException>(() =>
            _service.CompressImageAsync(imageBytes, quality));
    }

    // ==================== Tests Rate Limiting ====================

    [Fact]
    public void CheckRateLimit_NoPhotos_ReturnsTrue()
    {
        // Arrange
        var userId = Guid.NewGuid().ToString();

        // Act
        var result = _service.CheckRateLimit(userId);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void CheckRateLimit_UnderLimit_ReturnsTrue()
    {
        // Arrange
        var userId = Guid.NewGuid().ToString();

        // Enregistrer 3 photos (sous la limite de 5)
        for (int i = 0; i < 3; i++)
        {
            _service.RecordPhotoSent(userId);
        }

        // Act
        var result = _service.CheckRateLimit(userId);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void CheckRateLimit_AtLimit_ReturnsFalse()
    {
        // Arrange
        var userId = Guid.NewGuid().ToString();

        // Enregistrer 5 photos (exactement la limite)
        for (int i = 0; i < 5; i++)
        {
            _service.RecordPhotoSent(userId);
        }

        // Act
        var result = _service.CheckRateLimit(userId);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void CheckRateLimit_OverLimit_ReturnsFalse()
    {
        // Arrange
        var userId = Guid.NewGuid().ToString();

        // Enregistrer 7 photos (au-dessus de la limite)
        for (int i = 0; i < 7; i++)
        {
            _service.RecordPhotoSent(userId);
        }

        // Act
        var result = _service.CheckRateLimit(userId);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void CheckRateLimit_DifferentUsers_IndependentLimits()
    {
        // Arrange
        var userId1 = Guid.NewGuid().ToString();
        var userId2 = Guid.NewGuid().ToString();

        // User1 atteint la limite
        for (int i = 0; i < 5; i++)
        {
            _service.RecordPhotoSent(userId1);
        }

        // Act
        var result1 = _service.CheckRateLimit(userId1);
        var result2 = _service.CheckRateLimit(userId2);

        // Assert
        Assert.False(result1); // User1 bloqué
        Assert.True(result2);  // User2 OK
    }

    [Fact]
    public void RecordPhotoSent_FirstPhoto_RecordsSuccessfully()
    {
        // Arrange
        var userId = Guid.NewGuid().ToString();

        // Act
        _service.RecordPhotoSent(userId);

        // Assert - Vérifier que le rate limit est toujours OK
        var result = _service.CheckRateLimit(userId);
        Assert.True(result);
    }

    [Fact]
    public void RecordPhotoSent_MultiplePhotos_IncrementsCounter()
    {
        // Arrange
        var userId = Guid.NewGuid().ToString();

        // Act
        _service.RecordPhotoSent(userId);
        _service.RecordPhotoSent(userId);
        _service.RecordPhotoSent(userId);

        // Assert - 3 photos envoyées, devrait être OK (limite = 5)
        var result = _service.CheckRateLimit(userId);
        Assert.True(result);
    }

    [Fact]
    public void RecordPhotoSent_ReachingLimit_BlocksFurtherPhotos()
    {
        // Arrange
        var userId = Guid.NewGuid().ToString();

        // Act - Enregistrer exactement 5 photos
        for (int i = 0; i < 5; i++)
        {
            _service.RecordPhotoSent(userId);
        }

        // Assert
        var result = _service.CheckRateLimit(userId);
        Assert.False(result);
    }

    // ==================== Tests Logging ====================

    [Fact]
    public async Task ValidateImageAsync_NullImage_LogsWarning()
    {
        // Arrange
        byte[]? imageBytes = null;
        var maxSizeKb = 2048;

        // Act
        await _service.ValidateImageAsync(imageBytes!, maxSizeKb);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Image null")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task ValidateImageAsync_ImageTooLarge_LogsWarning()
    {
        // Arrange
        var imageBytes = GenerateTestImage(2000, 2000, ImageFormat.Jpeg);
        var maxSizeKb = 10;

        // Act
        await _service.ValidateImageAsync(imageBytes, maxSizeKb);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Image trop volumineuse")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task ValidateImageAsync_ValidImage_LogsInformation()
    {
        // Arrange
        var imageBytes = GenerateTestImage(800, 600, ImageFormat.Jpeg);
        var maxSizeKb = 2048;

        // Act
        await _service.ValidateImageAsync(imageBytes, maxSizeKb);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Image valide")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public void CheckRateLimit_OverLimit_LogsWarning()
    {
        // Arrange
        var userId = Guid.NewGuid().ToString();

        for (int i = 0; i < 5; i++)
        {
            _service.RecordPhotoSent(userId);
        }

        // Act
        _service.CheckRateLimit(userId);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Rate limit dépassé")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public void RecordPhotoSent_LogsInformation()
    {
        // Arrange
        var userId = Guid.NewGuid().ToString();

        // Act
        _service.RecordPhotoSent(userId);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Photo envoyée enregistrée")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}