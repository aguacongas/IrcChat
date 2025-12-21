using System.Net;
using System.Net.Http.Json;
using IrcChat.Client.Services;
using IrcChat.Shared.Models;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using RichardSzalay.MockHttp;

namespace IrcChat.Client.Tests.Services;

public class EphemeralPhotoServiceTests
{
    private readonly MockHttpMessageHandler _mockHttp;
    private readonly HttpClient _httpClient;
    private readonly Mock<ILogger<EphemeralPhotoService>> _loggerMock;
    private readonly Mock<IJSRuntime> _jsRuntimeMock;
    private readonly EphemeralPhotoService _service;

    public EphemeralPhotoServiceTests()
    {
        _mockHttp = new MockHttpMessageHandler();
        _httpClient = _mockHttp.ToHttpClient();
        _httpClient.BaseAddress = new Uri("https://localhost");

        _loggerMock = new Mock<ILogger<EphemeralPhotoService>>();
        _jsRuntimeMock = new Mock<IJSRuntime>();

        _service = new EphemeralPhotoService(
            _httpClient,
            _loggerMock.Object,
            _jsRuntimeMock.Object);
    }

    [Fact]
    public async Task ValidateImageFileAsync_WithValidFile_ShouldReturnTrue()
    {
        // Arrange
        var fileMock = new Mock<IBrowserFile>();
        fileMock.Setup(f => f.ContentType).Returns("image/jpeg");
        fileMock.Setup(f => f.Size).Returns(1024 * 1024); // 1MB

        // Act
        var result = await _service.ValidateImageFileAsync(fileMock.Object);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task ValidateImageFileAsync_WithInvalidMimeType_ShouldReturnFalse()
    {
        // Arrange
        var fileMock = new Mock<IBrowserFile>();
        fileMock.Setup(f => f.ContentType).Returns("application/pdf");
        fileMock.Setup(f => f.Size).Returns(1024);

        // Act
        var result = await _service.ValidateImageFileAsync(fileMock.Object);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task ValidateImageFileAsync_WithFileTooLarge_ShouldReturnFalse()
    {
        // Arrange
        var fileMock = new Mock<IBrowserFile>();
        fileMock.Setup(f => f.ContentType).Returns("image/jpeg");
        fileMock.Setup(f => f.Size).Returns(10 * 1024 * 1024); // 10MB

        // Act
        var result = await _service.ValidateImageFileAsync(fileMock.Object);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task UploadImageAsync_WithValidImage_ShouldReturnUrls()
    {
        // Arrange
        var userId = "test-user";
        var base64 = "data:image/jpeg;base64,/9j/4AAQSkZJRg==";

        var expectedResponse = new UploadEphemeralPhotoResponse
        {
            ImageUrl = "https://cloudinary.com/image.jpg",
            ThumbnailUrl = "https://cloudinary.com/thumb.jpg"
        };

        var request = _mockHttp
            .When(HttpMethod.Post, $"*/api/ephemeral-photos/{userId}/upload")
            .Respond(HttpStatusCode.OK, JsonContent.Create(expectedResponse));

        // Act
        var (imageUrl, thumbnailUrl) = await _service.UploadImageAsync(base64, userId);

        // Assert
        Assert.Equal("https://cloudinary.com/image.jpg", imageUrl);
        Assert.Equal("https://cloudinary.com/thumb.jpg", thumbnailUrl);
        Assert.Equal(1, _mockHttp.GetMatchCount(request));
    }

    [Fact]
    public async Task UploadImageAsync_WithServerError_ShouldThrow()
    {
        // Arrange
        var userId = "test-user";
        var base64 = "data:image/jpeg;base64,TEST";

        _mockHttp
            .When(HttpMethod.Post, $"*/api/ephemeral-photos/{userId}/upload")
            .Respond(HttpStatusCode.InternalServerError);

        // Act & Assert
        await Assert.ThrowsAsync<HttpRequestException>(async () =>
            await _service.UploadImageAsync(base64, userId));
    }

    [Fact]
    public async Task StartCameraAsync_Success_ShouldReturnTrue()
    {
        // Arrange
        var moduleMock = new Mock<IJSObjectReference>();
        _jsRuntimeMock
            .Setup(x => x.InvokeAsync<IJSObjectReference>("import", It.IsAny<object[]>()))
            .ReturnsAsync(moduleMock.Object);

        moduleMock
            .Setup(x => x.InvokeAsync<IJSVoidResult>("startCamera", It.IsAny<object[]>()))
            .ReturnsAsync((IJSVoidResult)null!);

        // Act
        var result = await _service.StartCameraAsync();

        // Assert
        Assert.True(result);
        moduleMock.Verify(
            x => x.InvokeAsync<IJSVoidResult>("startCamera", It.IsAny<object[]>()),
            Times.Once);
    }

    [Fact]
    public async Task StartCameraAsync_Error_ShouldReturnFalse()
    {
        // Arrange
        var moduleMock = new Mock<IJSObjectReference>();
        _jsRuntimeMock
            .Setup(x => x.InvokeAsync<IJSObjectReference>("import", It.IsAny<object[]>()))
            .ReturnsAsync(moduleMock.Object);

        moduleMock
            .Setup(x => x.InvokeAsync<IJSVoidResult>("startCamera", It.IsAny<object[]>()))
            .ThrowsAsync(new JSException("Camera error"));

        // Act
        var result = await _service.StartCameraAsync();

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task CapturePhotoAsync_ShouldReturnBase64()
    {
        // Arrange
        var moduleMock = new Mock<IJSObjectReference>();
        _jsRuntimeMock
            .Setup(x => x.InvokeAsync<IJSObjectReference>("import", It.IsAny<object[]>()))
            .ReturnsAsync(moduleMock.Object);

        var expectedBase64 = "data:image/jpeg;base64,CAPTURED_PHOTO";
        moduleMock
            .Setup(x => x.InvokeAsync<string>("capturePhotoFromVideo", It.IsAny<object[]>()))
            .ReturnsAsync(expectedBase64);

        // Act
        var result = await _service.CapturePhotoAsync("test-video-id");

        // Assert
        Assert.Equal(expectedBase64, result);
        moduleMock.Verify(
            x => x.InvokeAsync<string>("capturePhotoFromVideo", It.Is<object[]>(o => (string)o[0] == "test-video-id")),
            Times.Once);
    }

    [Fact]
    public async Task StopCameraAsync_ShouldInvokeJS()
    {
        // Arrange
        var moduleMock = new Mock<IJSObjectReference>();
        _jsRuntimeMock
            .Setup(x => x.InvokeAsync<IJSObjectReference>("import", It.IsAny<object[]>()))
            .ReturnsAsync(moduleMock.Object);

        moduleMock
            .Setup(x => x.InvokeAsync<IJSVoidResult>("stopCamera", It.IsAny<object[]>()))
            .ReturnsAsync((IJSVoidResult)null!);

        // Act
        await _service.StopCameraAsync();

        // Assert
        moduleMock.Verify(
            x => x.InvokeAsync<IJSVoidResult>("stopCamera", It.IsAny<object[]>()),
            Times.Once);
    }

    [Fact]
    public async Task IsCameraAvailableAsync_ShouldReturnJSResult()
    {
        // Arrange
        var moduleMock = new Mock<IJSObjectReference>();
        _jsRuntimeMock
            .Setup(x => x.InvokeAsync<IJSObjectReference>("import", It.IsAny<object[]>()))
            .ReturnsAsync(moduleMock.Object);

        moduleMock
            .Setup(x => x.InvokeAsync<bool>("isCameraAvailable", It.IsAny<object[]>()))
            .ReturnsAsync(true);

        // Act
        var result = await _service.IsCameraAvailableAsync();

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task BlockDevToolsAsync_ShouldInvokeJS()
    {
        // Arrange
        var moduleMock = new Mock<IJSObjectReference>();
        _jsRuntimeMock
            .Setup(x => x.InvokeAsync<IJSObjectReference>("import", It.IsAny<object[]>()))
            .ReturnsAsync(moduleMock.Object);

        moduleMock
            .Setup(x => x.InvokeAsync<IJSVoidResult>("blockDevTools", It.IsAny<object[]>()))
            .ReturnsAsync((IJSVoidResult)null!);

        // Act
        await _service.BlockDevToolsAsync();

        // Assert
        moduleMock.Verify(
            x => x.InvokeAsync<IJSVoidResult>("blockDevTools", It.IsAny<object[]>()),
            Times.Once);
    }

    [Fact]
    public async Task DisposeAsync_ShouldDisposeModules()
    {
        // Arrange
        var cameraModuleMock = new Mock<IJSObjectReference>();
        var ephemeralModuleMock = new Mock<IJSObjectReference>();

        _jsRuntimeMock
            .SetupSequence(x => x.InvokeAsync<IJSObjectReference>("import", It.IsAny<object[]>()))
            .ReturnsAsync(cameraModuleMock.Object)
            .ReturnsAsync(ephemeralModuleMock.Object);

        cameraModuleMock
            .Setup(x => x.InvokeAsync<IJSVoidResult>("stopCamera", It.IsAny<object[]>()))
            .ReturnsAsync((IJSVoidResult)null!);

        // Forcer le chargement des modules
        await _service.StartCameraAsync();
        await _service.BlockDevToolsAsync();

        // Act
        await _service.DisposeAsync();

        // Assert
        cameraModuleMock.Verify(x => x.DisposeAsync(), Times.Once);
        ephemeralModuleMock.Verify(x => x.DisposeAsync(), Times.Once);
    }
}