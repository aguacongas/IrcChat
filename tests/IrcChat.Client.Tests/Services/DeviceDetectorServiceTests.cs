// tests/IrcChat.Client.Tests/Services/DeviceDetectorServiceTests.cs
using IrcChat.Client.Services;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using Moq;
using Xunit;

namespace IrcChat.Client.Tests.Services;

public class DeviceDetectorServiceTests : IAsyncDisposable
{
    private readonly Mock<IJSRuntime> _jsRuntimeMock;
    private readonly Mock<IJSObjectReference> _moduleMock;
    private readonly Mock<ILogger<DeviceDetectorService>> _loggerMock;
    private readonly DeviceDetectorService _service;

    public DeviceDetectorServiceTests()
    {
        _jsRuntimeMock = new Mock<IJSRuntime>();
        _moduleMock = new Mock<IJSObjectReference>();
        _loggerMock = new Mock<ILogger<DeviceDetectorService>>();

        _jsRuntimeMock
            .Setup(x => x.InvokeAsync<IJSObjectReference>("import", It.IsAny<object[]>()))
            .ReturnsAsync(_moduleMock.Object);

        _service = new DeviceDetectorService(_jsRuntimeMock.Object, _loggerMock.Object);
    }

    [Fact]
    public async Task IsMobileDeviceAsync_WhenMobile_ShouldReturnTrue()
    {
        // Arrange
        _moduleMock
            .Setup(x => x.InvokeAsync<bool>("isMobileDevice", It.IsAny<object[]>()))
            .ReturnsAsync(true);

        // Act
        var result = await _service.IsMobileDeviceAsync();

        // Assert
        Assert.True(result);
        _moduleMock.Verify(x => x.InvokeAsync<bool>("isMobileDevice", It.IsAny<object[]>()), Times.Once);
    }

    [Fact]
    public async Task IsMobileDeviceAsync_WhenDesktop_ShouldReturnFalse()
    {
        // Arrange
        _moduleMock
            .Setup(x => x.InvokeAsync<bool>("isMobileDevice", It.IsAny<object[]>()))
            .ReturnsAsync(false);

        // Act
        var result = await _service.IsMobileDeviceAsync();

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task IsMobileDeviceAsync_WhenJSError_ShouldReturnFalseAndLogWarning()
    {
        // Arrange
        _moduleMock
            .Setup(x => x.InvokeAsync<bool>("isMobileDevice", It.IsAny<object[]>()))
            .ThrowsAsync(new JSException("JS Error"));

        // Act
        var result = await _service.IsMobileDeviceAsync();

        // Assert
        Assert.False(result);
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task GetScreenWidthAsync_ShouldReturnCorrectWidth()
    {
        // Arrange
        _moduleMock
            .Setup(x => x.InvokeAsync<int>("getScreenWidth", It.IsAny<object[]>()))
            .ReturnsAsync(1920);

        // Act
        var result = await _service.GetScreenWidthAsync();

        // Assert
        Assert.Equal(1920, result);
        _moduleMock.Verify(x => x.InvokeAsync<int>("getScreenWidth", It.IsAny<object[]>()), Times.Once);
    }

    [Fact]
    public async Task GetScreenWidthAsync_WhenJSError_ShouldReturnDefaultWidth()
    {
        // Arrange
        _moduleMock
            .Setup(x => x.InvokeAsync<int>("getScreenWidth", It.IsAny<object[]>()))
            .ThrowsAsync(new JSException("JS Error"));

        // Act
        var result = await _service.GetScreenWidthAsync();

        // Assert
        Assert.Equal(1024, result);
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task IsMobileDeviceAsync_CalledMultipleTimes_ShouldReuseModule()
    {
        // Arrange
        _moduleMock
            .Setup(x => x.InvokeAsync<bool>("isMobileDevice", It.IsAny<object[]>()))
            .ReturnsAsync(false);

        // Act
        await _service.IsMobileDeviceAsync();
        await _service.IsMobileDeviceAsync();
        await _service.IsMobileDeviceAsync();

        // Assert
        _jsRuntimeMock.Verify(
            x => x.InvokeAsync<IJSObjectReference>("import", It.IsAny<object[]>()),
            Times.Once); // Module importé une seule fois
        _moduleMock.Verify(
            x => x.InvokeAsync<bool>("isMobileDevice", It.IsAny<object[]>()),
            Times.Exactly(3)); // Fonction appelée 3 fois
    }

    [Fact]
    public async Task DisposeAsync_ShouldDisposeModule()
    {
        // Arrange
        _moduleMock
            .Setup(x => x.DisposeAsync())
            .Returns(ValueTask.CompletedTask);

        // Initialiser le module
        await _service.IsMobileDeviceAsync();

        // Act
        await _service.DisposeAsync();

        // Assert
        _moduleMock.Verify(x => x.DisposeAsync(), Times.Once);
    }

    public async ValueTask DisposeAsync()
    {
        await _service.DisposeAsync();
        GC.SuppressFinalize(this);
    }
}