// tests/IrcChat.Client.Tests/Services/DeviceDetectorServiceTests.cs
using IrcChat.Client.Services;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace IrcChat.Client.Tests.Services;

public class DeviceDetectorServiceTests : IAsyncDisposable
{
    private readonly Mock<IJSRuntime> jsRuntimeMock;
    private readonly Mock<IJSObjectReference> moduleMock;
    private readonly Mock<ILogger<DeviceDetectorService>> loggerMock;
    private readonly DeviceDetectorService service;

    public DeviceDetectorServiceTests()
    {
        jsRuntimeMock = new Mock<IJSRuntime>();
        moduleMock = new Mock<IJSObjectReference>();
        loggerMock = new Mock<ILogger<DeviceDetectorService>>();

        jsRuntimeMock
            .Setup(x => x.InvokeAsync<IJSObjectReference>("import", It.IsAny<object[]>()))
            .ReturnsAsync(moduleMock.Object);

        service = new DeviceDetectorService(jsRuntimeMock.Object, loggerMock.Object);
    }

    [Fact]
    public async Task IsMobileDeviceAsync_WhenMobile_ShouldReturnTrue()
    {
        // Arrange
        moduleMock
            .Setup(x => x.InvokeAsync<bool>("isMobileDevice", It.IsAny<object[]>()))
            .ReturnsAsync(true);

        // Act
        var result = await service.IsMobileDeviceAsync();

        // Assert
        Assert.True(result);
        moduleMock.Verify(x => x.InvokeAsync<bool>("isMobileDevice", It.IsAny<object[]>()), Times.Once);
    }

    [Fact]
    public async Task IsMobileDeviceAsync_WhenDesktop_ShouldReturnFalse()
    {
        // Arrange
        moduleMock
            .Setup(x => x.InvokeAsync<bool>("isMobileDevice", It.IsAny<object[]>()))
            .ReturnsAsync(false);

        // Act
        var result = await service.IsMobileDeviceAsync();

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task IsMobileDeviceAsync_WhenJSError_ShouldReturnFalseAndLogWarning()
    {
        // Arrange
        moduleMock
            .Setup(x => x.InvokeAsync<bool>("isMobileDevice", It.IsAny<object[]>()))
            .ThrowsAsync(new JSException("JS Error"));

        // Act
        var result = await service.IsMobileDeviceAsync();

        // Assert
        Assert.False(result);
        loggerMock.Verify(
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
        moduleMock
            .Setup(x => x.InvokeAsync<int>("getScreenWidth", It.IsAny<object[]>()))
            .ReturnsAsync(1920);

        // Act
        var result = await service.GetScreenWidthAsync();

        // Assert
        Assert.Equal(1920, result);
        moduleMock.Verify(x => x.InvokeAsync<int>("getScreenWidth", It.IsAny<object[]>()), Times.Once);
    }

    [Fact]
    public async Task GetScreenWidthAsync_WhenJSError_ShouldReturnDefaultWidth()
    {
        // Arrange
        moduleMock
            .Setup(x => x.InvokeAsync<int>("getScreenWidth", It.IsAny<object[]>()))
            .ThrowsAsync(new JSException("JS Error"));

        // Act
        var result = await service.GetScreenWidthAsync();

        // Assert
        Assert.Equal(1024, result);
        loggerMock.Verify(
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
        moduleMock
            .Setup(x => x.InvokeAsync<bool>("isMobileDevice", It.IsAny<object[]>()))
            .ReturnsAsync(false);

        // Act
        await service.IsMobileDeviceAsync();
        await service.IsMobileDeviceAsync();
        await service.IsMobileDeviceAsync();

        // Assert
        jsRuntimeMock.Verify(
            x => x.InvokeAsync<IJSObjectReference>("import", It.IsAny<object[]>()),
            Times.Once); // Module importé une seule fois
        moduleMock.Verify(
            x => x.InvokeAsync<bool>("isMobileDevice", It.IsAny<object[]>()),
            Times.Exactly(3)); // Fonction appelée 3 fois
    }

    [Fact]
    public async Task DisposeAsync_ShouldDisposeModule()
    {
        // Arrange
        moduleMock
            .Setup(x => x.DisposeAsync())
            .Returns(ValueTask.CompletedTask);

        // Initialiser le module
        await service.IsMobileDeviceAsync();

        // Act
        await service.DisposeAsync();

        // Assert
        moduleMock.Verify(x => x.DisposeAsync(), Times.Once);
    }

    // Tests supplémentaires pour DeviceDetectorService
    // Ajouter à tests/IrcChat.Client.Tests/Services/DeviceDetectorServiceTests.cs
    [Fact]
    public async Task EnsureInitializedAsync_WhenModuleLoadFails_ShouldLogErrorAndThrow()
    {
        // Arrange
        var jsRuntimeMock = new Mock<IJSRuntime>();
        var loggerMock = new Mock<ILogger<DeviceDetectorService>>();

        jsRuntimeMock
            .Setup(x => x.InvokeAsync<IJSObjectReference>("import", It.IsAny<object[]>()))
            .ThrowsAsync(new JSException("Module load failed"));

        var service = new DeviceDetectorService(jsRuntimeMock.Object, loggerMock.Object);

        // Act & Assert
        await Assert.ThrowsAsync<JSException>(async () => await service.IsMobileDeviceAsync());

        loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task DisposeAsync_WhenModuleNotInitialized_ShouldNotThrow()
    {
        // Arrange
        var jsRuntimeMock = new Mock<IJSRuntime>();
        var loggerMock = new Mock<ILogger<DeviceDetectorService>>();
        var service = new DeviceDetectorService(jsRuntimeMock.Object, loggerMock.Object);

        // Act & Assert - Ne devrait pas lever d'exception
        await service.DisposeAsync();

        Assert.True(true);
    }

    [Fact]
    public async Task DisposeAsync_WhenDisposeThrows_ShouldLogWarning()
    {
        // Arrange
        var jsRuntimeMock = new Mock<IJSRuntime>();
        var moduleMock = new Mock<IJSObjectReference>();
        var loggerMock = new Mock<ILogger<DeviceDetectorService>>();

        jsRuntimeMock
            .Setup(x => x.InvokeAsync<IJSObjectReference>("import", It.IsAny<object[]>()))
            .ReturnsAsync(moduleMock.Object);

        moduleMock
            .Setup(x => x.InvokeAsync<bool>("isMobileDevice", It.IsAny<object[]>()))
            .ReturnsAsync(false);

        moduleMock
            .Setup(x => x.DisposeAsync())
            .Throws(new Exception("Dispose error"));

        var service = new DeviceDetectorService(jsRuntimeMock.Object, loggerMock.Object);
        await service.IsMobileDeviceAsync();

        // Act
        await service.DisposeAsync();

        // Assert
        loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Theory]
    [InlineData(320)]
    [InlineData(768)]
    [InlineData(1024)]
    [InlineData(1920)]
    [InlineData(2560)]
    public async Task GetScreenWidthAsync_VariousWidths_ShouldReturnCorrectValue(int width)
    {
        // Arrange
        var jsRuntimeMock = new Mock<IJSRuntime>();
        var moduleMock = new Mock<IJSObjectReference>();
        var loggerMock = new Mock<ILogger<DeviceDetectorService>>();

        jsRuntimeMock
            .Setup(x => x.InvokeAsync<IJSObjectReference>("import", It.IsAny<object[]>()))
            .ReturnsAsync(moduleMock.Object);

        moduleMock
            .Setup(x => x.InvokeAsync<int>("getScreenWidth", It.IsAny<object[]>()))
            .ReturnsAsync(width);

        var service = new DeviceDetectorService(jsRuntimeMock.Object, loggerMock.Object);

        // Act
        var result = await service.GetScreenWidthAsync();

        // Assert
        Assert.Equal(width, result);
    }

    public async ValueTask DisposeAsync()
    {
        await service.DisposeAsync();
        GC.SuppressFinalize(this);
    }
}