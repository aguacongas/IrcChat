using System.Net;
using System.Net.Http.Json;
using IrcChat.Client.Services;
using IrcChat.Shared.Models;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using Moq.Protected;

namespace IrcChat.Client.Tests.Services;

/// <summary>
/// Tests pour EphemeralPhotoService
/// </summary>
public class EphemeralPhotoServiceTests : IDisposable
{
    private readonly Mock<HttpMessageHandler> _httpHandlerMock;
    private readonly HttpClient _httpClient;
    private readonly Mock<ILogger<EphemeralPhotoService>> _loggerMock;
    private readonly Mock<IJSRuntime> _jsRuntimeMock;
    private readonly Mock<IJSObjectReference> _cameraModuleMock;
    private readonly Mock<IJSObjectReference> _ephemeralModuleMock;
    private readonly EphemeralPhotoService _service;

    public EphemeralPhotoServiceTests()
    {
        _httpHandlerMock = new Mock<HttpMessageHandler>();
        _httpClient = new HttpClient(_httpHandlerMock.Object)
        {
            BaseAddress = new Uri("https://localhost:7001")
        };
        _loggerMock = new Mock<ILogger<EphemeralPhotoService>>();
        _jsRuntimeMock = new Mock<IJSRuntime>();
        _cameraModuleMock = new Mock<IJSObjectReference>();
        _ephemeralModuleMock = new Mock<IJSObjectReference>();

        // Setup des modules JS par défaut
        _jsRuntimeMock
            .Setup(x => x.InvokeAsync<IJSObjectReference>("import", It.Is<object[]>(o => o.Length == 1 && (string)o[0] == "./js/camera.js")))
            .ReturnsAsync(_cameraModuleMock.Object);

        _jsRuntimeMock
            .Setup(x => x.InvokeAsync<IJSObjectReference>("import", It.Is<object[]>(o => o.Length == 1 && (string)o[0] == "./js/ephemeralPhoto.js")))
            .ReturnsAsync(_ephemeralModuleMock.Object);

        _service = new EphemeralPhotoService(_httpClient, _loggerMock.Object, _jsRuntimeMock.Object);
    }

    public void Dispose()
    {
        _httpClient.Dispose();
        GC.SuppressFinalize(this);
    }

    // ==================== Tests Validation ====================

    [Fact]
    public async Task ValidateImageFileAsync_ValidJpegFile_ReturnsTrue()
    {
        // Arrange
        var mockFile = new Mock<IBrowserFile>();
        mockFile.Setup(f => f.Name).Returns("photo.jpg");
        mockFile.Setup(f => f.Size).Returns(1_000_000); // 1MB
        mockFile.Setup(f => f.ContentType).Returns("image/jpeg");

        // Act
        var result = await _service.ValidateImageFileAsync(mockFile.Object);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task ValidateImageFileAsync_ValidPngFile_ReturnsTrue()
    {
        // Arrange
        var mockFile = new Mock<IBrowserFile>();
        mockFile.Setup(f => f.Name).Returns("photo.png");
        mockFile.Setup(f => f.Size).Returns(1_500_000); // 1.5MB
        mockFile.Setup(f => f.ContentType).Returns("image/png");

        // Act
        var result = await _service.ValidateImageFileAsync(mockFile.Object);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task ValidateImageFileAsync_ValidWebpFile_ReturnsTrue()
    {
        // Arrange
        var mockFile = new Mock<IBrowserFile>();
        mockFile.Setup(f => f.Name).Returns("photo.webp");
        mockFile.Setup(f => f.Size).Returns(500_000); // 500KB
        mockFile.Setup(f => f.ContentType).Returns("image/webp");

        // Act
        var result = await _service.ValidateImageFileAsync(mockFile.Object);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task ValidateImageFileAsync_InvalidMimeType_ReturnsFalse()
    {
        // Arrange
        var mockFile = new Mock<IBrowserFile>();
        mockFile.Setup(f => f.Name).Returns("document.pdf");
        mockFile.Setup(f => f.Size).Returns(1_000_000);
        mockFile.Setup(f => f.ContentType).Returns("application/pdf");

        // Act
        var result = await _service.ValidateImageFileAsync(mockFile.Object);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task ValidateImageFileAsync_FileTooLarge_ReturnsFalse()
    {
        // Arrange
        var mockFile = new Mock<IBrowserFile>();
        mockFile.Setup(f => f.Name).Returns("photo.jpg");
        mockFile.Setup(f => f.Size).Returns(3_000_000); // 3MB > 2MB max
        mockFile.Setup(f => f.ContentType).Returns("image/jpeg");

        // Act
        var result = await _service.ValidateImageFileAsync(mockFile.Object);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task ValidateImageFileAsync_ExactlyMaxSize_ReturnsTrue()
    {
        // Arrange
        var mockFile = new Mock<IBrowserFile>();
        mockFile.Setup(f => f.Name).Returns("photo.jpg");
        mockFile.Setup(f => f.Size).Returns(2_097_152); // Exactement 2MB
        mockFile.Setup(f => f.ContentType).Returns("image/jpeg");

        // Act
        var result = await _service.ValidateImageFileAsync(mockFile.Object);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task ValidateImageFileAsync_CaseInsensitiveMimeType_ReturnsTrue()
    {
        // Arrange
        var mockFile = new Mock<IBrowserFile>();
        mockFile.Setup(f => f.Name).Returns("photo.jpg");
        mockFile.Setup(f => f.Size).Returns(1_000_000);
        mockFile.Setup(f => f.ContentType).Returns("IMAGE/JPEG"); // Uppercase

        // Act
        var result = await _service.ValidateImageFileAsync(mockFile.Object);

        // Assert
        Assert.True(result);
    }

    // ==================== Tests Upload ====================

    [Fact]
    public async Task UploadImageAsync_Success_ReturnsUrls()
    {
        // Arrange
        var userId = "user123";
        var imageBase64 = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg==";
        var expectedResponse = new UploadEphemeralPhotoResponse
        {
            ImageUrl = "https://example.com/images/abc123.jpg",
            ThumbnailUrl = "https://example.com/thumbs/abc123_thumb.jpg"
        };

        _httpHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req =>
                    req.Method == HttpMethod.Post &&
                    req.RequestUri!.ToString().Contains($"/api/ephemeral-photos/{userId}/upload")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = JsonContent.Create(expectedResponse)
            });

        // Act
        var (imageUrl, thumbnailUrl) = await _service.UploadImageAsync(imageBase64, userId);

        // Assert
        Assert.Equal(expectedResponse.ImageUrl, imageUrl);
        Assert.Equal(expectedResponse.ThumbnailUrl, thumbnailUrl);
    }

    [Fact]
    public async Task UploadImageAsync_WithDataUrlPrefix_RemovesPrefix()
    {
        // Arrange
        var userId = "user123";
        var base64Data = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg==";
        var imageBase64 = $"data:image/png;base64,{base64Data}";
        var expectedResponse = new UploadEphemeralPhotoResponse
        {
            ImageUrl = "https://example.com/images/abc123.jpg",
            ThumbnailUrl = "https://example.com/thumbs/abc123_thumb.jpg"
        };

        UploadEphemeralPhotoRequest? capturedRequest = null;

        _httpHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req =>
                    req.Method == HttpMethod.Post &&
                    req.RequestUri!.ToString().Contains($"/api/ephemeral-photos/{userId}/upload")),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>(async (req, _)
                => capturedRequest = await req.Content!.ReadFromJsonAsync<UploadEphemeralPhotoRequest>(cancellationToken: _))
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = JsonContent.Create(expectedResponse)
            });

        // Act
        await _service.UploadImageAsync(imageBase64, userId);

        // Assert
        Assert.NotNull(capturedRequest);
        Assert.Equal(base64Data, capturedRequest.ImageBase64);
        Assert.DoesNotContain("data:image", capturedRequest.ImageBase64);
    }

    [Fact]
    public async Task UploadImageAsync_HttpError_ThrowsException()
    {
        // Arrange
        var userId = "user123";
        var imageBase64 = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg==";

        _httpHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.BadRequest,
                Content = new StringContent("Invalid image data")
            });

        // Act & Assert
        var ex = await Assert.ThrowsAsync<HttpRequestException>(() =>
            _service.UploadImageAsync(imageBase64, userId));

        Assert.Contains("Upload failed", ex.Message);
    }

    [Fact]
    public async Task UploadImageAsync_NullResponse_ThrowsException()
    {
        // Arrange
        var userId = "user123";
        var imageBase64 = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg==";

        _httpHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent("null")
            });

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.UploadImageAsync(imageBase64, userId));
    }

    // ==================== Tests Caméra ====================

    [Fact]
    public async Task StartCameraAsync_Success_ReturnsTrue()
    {
        // Arrange
        _cameraModuleMock
            .Setup(x => x.InvokeAsync<IJSVoidResult>("startCamera", It.IsAny<object[]>()))
            .ReturnsAsync((IJSVoidResult)null!);

        // Act
        var result = await _service.StartCameraAsync();

        // Assert
        Assert.True(result);
        _cameraModuleMock.Verify(x => x.InvokeAsync<IJSVoidResult>("startCamera", It.IsAny<object[]>()), Times.Once);
    }

    [Fact]
    public async Task StartCameraAsync_JsError_ReturnsFalse()
    {
        // Arrange
        _cameraModuleMock
            .Setup(x => x.InvokeAsync<IJSVoidResult>("startCamera", It.IsAny<object[]>()))
            .ThrowsAsync(new JSException("Camera not available"));

        // Act
        var result = await _service.StartCameraAsync();

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task StopCameraAsync_Success_CallsJsMethod()
    {
        // Arrange
        _cameraModuleMock
            .Setup(x => x.InvokeAsync<IJSVoidResult>("stopCamera", It.IsAny<object[]>()))
            .ReturnsAsync((IJSVoidResult)null!);

        // Act
        await _service.StopCameraAsync();

        // Assert
        _cameraModuleMock.Verify(x => x.InvokeAsync<IJSVoidResult>("stopCamera", It.IsAny<object[]>()), Times.Once);
    }

    [Fact]
    public async Task StopCameraAsync_JsError_LogsError()
    {
        // Arrange
        _cameraModuleMock
            .Setup(x => x.InvokeAsync<IJSVoidResult>("stopCamera", It.IsAny<object[]>()))
            .ThrowsAsync(new JSException("Error stopping camera"));

        // Act
        await _service.StopCameraAsync();

        // Assert - Vérifie que l'erreur est loggée
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Erreur lors de l'arrêt de la caméra")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task AttachCameraToVideoAsync_Success_AttachesStream()
    {
        // Arrange
        var videoElementId = "video-preview";
        var mockStream = new Mock<IJSObjectReference>();

        _cameraModuleMock
            .Setup(x => x.InvokeAsync<IJSObjectReference>("startCamera", It.IsAny<object[]>()))
            .ReturnsAsync(mockStream.Object);

        _cameraModuleMock
            .Setup(x => x.InvokeAsync<IJSVoidResult>("attachStreamToVideo", It.IsAny<object[]>()))
            .ReturnsAsync((IJSVoidResult)null!);

        // Act
        await _service.AttachCameraToVideoAsync(videoElementId);

        // Assert
        _cameraModuleMock.Verify(
            x => x.InvokeAsync<IJSObjectReference>("startCamera", It.IsAny<object[]>()),
            Times.Once);

        _cameraModuleMock.Verify(
            x => x.InvokeAsync<IJSVoidResult>("attachStreamToVideo", It.Is<object[]>(o =>
                o.Length == 2 &&
                (string)o[0] == videoElementId &&
                o[1] == mockStream.Object)),
            Times.Once);
    }

    [Fact]
    public async Task AttachCameraToVideoAsync_JsError_ThrowsException()
    {
        // Arrange
        var videoElementId = "video-preview";

        _cameraModuleMock
            .Setup(x => x.InvokeAsync<IJSObjectReference>("startCamera", It.IsAny<object[]>()))
            .ThrowsAsync(new JSException("Camera error"));

        // Act & Assert
        await Assert.ThrowsAsync<JSException>(() =>
            _service.AttachCameraToVideoAsync(videoElementId));
    }

    [Fact]
    public async Task CapturePhotoAsync_Success_ReturnsBase64()
    {
        // Arrange
        var videoElementId = "video-preview";
        var expectedBase64 = "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg==";

        _cameraModuleMock
            .Setup(x => x.InvokeAsync<string>("capturePhotoFromVideo", It.IsAny<object[]>()))
            .ReturnsAsync(expectedBase64);

        // Act
        var result = await _service.CapturePhotoAsync(videoElementId);

        // Assert
        Assert.Equal(expectedBase64, result);
        _cameraModuleMock.Verify(
            x => x.InvokeAsync<string>("capturePhotoFromVideo", It.Is<object[]>(o =>
                o.Length == 1 && (string)o[0] == videoElementId)),
            Times.Once);
    }

    [Fact]
    public async Task CapturePhotoAsync_JsError_ThrowsException()
    {
        // Arrange
        var videoElementId = "video-preview";

        _cameraModuleMock
            .Setup(x => x.InvokeAsync<string>("capturePhotoFromVideo", It.IsAny<object[]>()))
            .ThrowsAsync(new JSException("Capture failed"));

        // Act & Assert
        await Assert.ThrowsAsync<JSException>(() =>
            _service.CapturePhotoAsync(videoElementId));
    }

    [Fact]
    public async Task IsCameraAvailableAsync_CameraAvailable_ReturnsTrue()
    {
        // Arrange
        _cameraModuleMock
            .Setup(x => x.InvokeAsync<bool>("isCameraAvailable", It.IsAny<object[]>()))
            .ReturnsAsync(true);

        // Act
        var result = await _service.IsCameraAvailableAsync();

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task IsCameraAvailableAsync_CameraNotAvailable_ReturnsFalse()
    {
        // Arrange
        _cameraModuleMock
            .Setup(x => x.InvokeAsync<bool>("isCameraAvailable", It.IsAny<object[]>()))
            .ReturnsAsync(false);

        // Act
        var result = await _service.IsCameraAvailableAsync();

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task IsCameraAvailableAsync_JsError_ReturnsFalse()
    {
        // Arrange
        _cameraModuleMock
            .Setup(x => x.InvokeAsync<bool>("isCameraAvailable", It.IsAny<object[]>()))
            .ThrowsAsync(new JSException("Error checking camera"));

        // Act
        var result = await _service.IsCameraAvailableAsync();

        // Assert
        Assert.False(result);
    }

    // ==================== Tests Sécurité ====================

    [Fact]
    public async Task BlockDevToolsAsync_Success_CallsJsMethod()
    {
        // Arrange
        _ephemeralModuleMock
            .Setup(x => x.InvokeAsync<IJSVoidResult>("blockDevTools", It.IsAny<object[]>()))
            .ReturnsAsync((IJSVoidResult)null!);

        // Act
        await _service.BlockDevToolsAsync();

        // Assert
        _ephemeralModuleMock.Verify(x => x.InvokeAsync<IJSVoidResult>("blockDevTools", It.IsAny<object[]>()), Times.Once);
    }

    [Fact]
    public async Task BlockDevToolsAsync_JsError_LogsError()
    {
        // Arrange
        _ephemeralModuleMock
            .Setup(x => x.InvokeAsync<IJSVoidResult>("blockDevTools", It.IsAny<object[]>()))
            .ThrowsAsync(new JSException("DevTools error"));

        // Act
        await _service.BlockDevToolsAsync();

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Erreur lors du blocage DevTools")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task DetectScreenshotAsync_Success_CallsJsMethod()
    {
        // Arrange
        _ephemeralModuleMock
            .Setup(x => x.InvokeAsync<IJSVoidResult>("detectScreenshot", It.IsAny<object[]>()))
            .ReturnsAsync((IJSVoidResult)null!);

        // Act
        await _service.DetectScreenshotAsync();

        // Assert
        _ephemeralModuleMock.Verify(x => x.InvokeAsync<IJSVoidResult>("detectScreenshot", It.IsAny<object[]>()), Times.Once);
    }

    [Fact]
    public async Task DetectScreenshotAsync_JsError_LogsError()
    {
        // Arrange
        _ephemeralModuleMock
            .Setup(x => x.InvokeAsync<IJSVoidResult>("detectScreenshot", It.IsAny<object[]>()))
            .ThrowsAsync(new JSException("Screenshot detection error"));

        // Act
        await _service.DetectScreenshotAsync();

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Erreur lors de la détection screenshot")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task DestroyImageDataAsync_Success_CallsJsMethod()
    {
        // Arrange
        var elementId = "image-preview";

        _ephemeralModuleMock
            .Setup(x => x.InvokeAsync<IJSVoidResult>("destroyImageData", It.IsAny<object[]>()))
            .ReturnsAsync((IJSVoidResult)null!);

        // Act
        await _service.DestroyImageDataAsync(elementId);

        // Assert
        _ephemeralModuleMock.Verify(
            x => x.InvokeAsync<IJSVoidResult>("destroyImageData", It.Is<object[]>(o =>
                o.Length == 1 && (string)o[0] == elementId)),
            Times.Once);
    }

    [Fact]
    public async Task DestroyImageDataAsync_JsError_LogsError()
    {
        // Arrange
        var elementId = "image-preview";

        _ephemeralModuleMock
            .Setup(x => x.InvokeAsync<IJSVoidResult>("destroyImageData", It.IsAny<object[]>()))
            .ThrowsAsync(new JSException("Destroy error"));

        // Act
        await _service.DestroyImageDataAsync(elementId);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Erreur lors de la destruction de l'image")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    // ==================== Tests Dispose ====================

    [Fact]
    public async Task DisposeAsync_StopsCameraAndDisposesModules()
    {
        // Arrange
        _cameraModuleMock
            .Setup(x => x.InvokeAsync<IJSVoidResult>("stopCamera", It.IsAny<object[]>()))
            .ReturnsAsync((IJSVoidResult)null!);

        _cameraModuleMock
            .Setup(x => x.DisposeAsync())
            .Returns(ValueTask.CompletedTask);

        _ephemeralModuleMock
            .Setup(x => x.DisposeAsync())
            .Returns(ValueTask.CompletedTask);

        // Forcer le chargement des modules
        await _service.StartCameraAsync();
        await _service.BlockDevToolsAsync();

        // Act
        await _service.DisposeAsync();

        // Assert
        _cameraModuleMock.Verify(x => x.InvokeAsync<IJSVoidResult>("stopCamera", It.IsAny<object[]>()), Times.Once);
        _cameraModuleMock.Verify(x => x.DisposeAsync(), Times.Once);
        _ephemeralModuleMock.Verify(x => x.DisposeAsync(), Times.Once);
    }

    [Fact]
    public async Task DisposeAsync_CameraModuleError_LogsWarning()
    {
        // Arrange
        _cameraModuleMock
            .Setup(x => x.InvokeAsync<IJSVoidResult>("stopCamera", It.IsAny<object[]>()))
            .ThrowsAsync(new JSException("Stop camera error"));

        // Forcer le chargement du module
        await _service.IsCameraAvailableAsync();

        // Act
        await _service.DisposeAsync();

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Erreur lors du dispose du module camera.js")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task DisposeAsync_EphemeralModuleError_LogsWarning()
    {
        // Arrange
        _ephemeralModuleMock
            .Setup(x => x.DisposeAsync())
            .ThrowsAsync(new JSException("Dispose error"));

        // Forcer le chargement du module
        await _service.BlockDevToolsAsync();

        // Act
        await _service.DisposeAsync();

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Erreur lors du dispose du module ephemeralPhoto.js")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}