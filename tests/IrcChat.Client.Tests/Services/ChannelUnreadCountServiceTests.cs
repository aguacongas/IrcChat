// tests/IrcChat.Client.Tests/Services/ChannelUnreadCountServiceTests.cs
using IrcChat.Client.Services;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace IrcChat.Client.Tests.Services;

public class ChannelUnreadCountServiceTests
{
    private readonly Mock<IJSRuntime> jsRuntimeMock;
    private readonly Mock<IJSObjectReference> moduleMock;
    private readonly Mock<ILogger<ChannelUnreadCountService>> loggerMock;
    private readonly ChannelUnreadCountService service;

    public ChannelUnreadCountServiceTests()
    {
        jsRuntimeMock = new Mock<IJSRuntime>();
        moduleMock = new Mock<IJSObjectReference>();
        loggerMock = new Mock<ILogger<ChannelUnreadCountService>>();

        jsRuntimeMock
            .Setup(x => x.InvokeAsync<IJSObjectReference>(
                "import",
                It.Is<object[]>(o => o.Length == 1 && (string)o[0] == "./js/channel-unread-count.js")))
            .ReturnsAsync(moduleMock.Object);

        service = new ChannelUnreadCountService(jsRuntimeMock.Object, loggerMock.Object);
    }

    [Fact]
    public async Task IncrementCountAsync_WithNewChannel_ShouldSetCountTo1()
    {
        // Arrange
        const string channel = "general";
        moduleMock
            .Setup(x => x.InvokeAsync<IJSVoidResult>(
                "saveUnreadCounts",
                It.IsAny<object[]>()))
            .ReturnsAsync((IJSVoidResult)null!);

        // Act
        await service.IncrementCountAsync(channel);

        // Assert
        var count = service.GetCount(channel);
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task IncrementCountAsync_WithExistingChannel_ShouldIncrementCount()
    {
        // Arrange
        const string channel = "general";
        moduleMock
            .Setup(x => x.InvokeAsync<IJSVoidResult>(
                "saveUnreadCounts",
                It.IsAny<object[]>()))
            .ReturnsAsync((IJSVoidResult)null!);

        await service.IncrementCountAsync(channel);
        await service.IncrementCountAsync(channel);

        // Act
        await service.IncrementCountAsync(channel);

        // Assert
        var count = service.GetCount(channel);
        Assert.Equal(3, count);
    }

    [Fact]
    public async Task IncrementCountAsync_ShouldSaveToStorage()
    {
        // Arrange
        const string channel = "general";
        moduleMock
            .Setup(x => x.InvokeAsync<IJSVoidResult>(
                "saveUnreadCounts",
                It.IsAny<object[]>()))
            .ReturnsAsync((IJSVoidResult)null!);

        // Act
        await service.IncrementCountAsync(channel);

        // Assert
        moduleMock.Verify(
            x => x.InvokeAsync<IJSVoidResult>(
                "saveUnreadCounts",
                It.Is<object[]>(o => o.Length == 1)),
            Times.Once);
    }

    [Fact]
    public async Task IncrementCountAsync_ShouldTriggerOnCountsChanged()
    {
        // Arrange
        const string channel = "general";
        var eventTriggered = false;
        service.OnCountsChanged += () => eventTriggered = true;

        moduleMock
            .Setup(x => x.InvokeAsync<IJSVoidResult>(
                "saveUnreadCounts",
                It.IsAny<object[]>()))
            .ReturnsAsync((IJSVoidResult)null!);

        // Act
        await service.IncrementCountAsync(channel);

        // Assert
        Assert.True(eventTriggered);
    }

    [Fact]
    public async Task IncrementCountAsync_WithNullOrEmpty_ShouldNotIncrement()
    {
        // Arrange
        moduleMock
            .Setup(x => x.InvokeAsync<IJSVoidResult>(
                "saveUnreadCounts",
                It.IsAny<object[]>()))
            .ReturnsAsync((IJSVoidResult)null!);

        // Act
        await service.IncrementCountAsync(null!);
        await service.IncrementCountAsync(string.Empty);
        await service.IncrementCountAsync("   ");

        // Assert
        moduleMock.Verify(
            x => x.InvokeAsync<IJSVoidResult>(
                "saveUnreadCounts",
                It.IsAny<object[]>()),
            Times.Never);
    }

    [Fact]
    public async Task ResetCountAsync_WithExistingChannel_ShouldSetCountTo0()
    {
        // Arrange
        const string channel = "general";
        moduleMock
            .Setup(x => x.InvokeAsync<IJSVoidResult>(
                "saveUnreadCounts",
                It.IsAny<object[]>()))
            .ReturnsAsync((IJSVoidResult)null!);

        await service.IncrementCountAsync(channel);
        await service.IncrementCountAsync(channel);

        // Act
        await service.ResetCountAsync(channel);

        // Assert
        var count = service.GetCount(channel);
        Assert.Equal(0, count);
    }

    [Fact]
    public async Task ResetCountAsync_ShouldSaveToStorage()
    {
        // Arrange
        const string channel = "general";
        moduleMock
            .Setup(x => x.InvokeAsync<IJSVoidResult>(
                "saveUnreadCounts",
                It.IsAny<object[]>()))
            .ReturnsAsync((IJSVoidResult)null!);

        await service.IncrementCountAsync(channel);

        // Act
        await service.ResetCountAsync(channel);

        // Assert
        moduleMock.Verify(
            x => x.InvokeAsync<IJSVoidResult>(
                "saveUnreadCounts",
                It.Is<object[]>(o => o.Length == 1)),
            Times.AtLeast(2)); // Once for increment, once for reset
    }

    [Fact]
    public async Task ResetCountAsync_ShouldTriggerOnCountsChanged()
    {
        // Arrange
        const string channel = "general";
        var eventTriggerCount = 0;
        service.OnCountsChanged += () => eventTriggerCount++;

        moduleMock
            .Setup(x => x.InvokeAsync<IJSVoidResult>(
                "saveUnreadCounts",
                It.IsAny<object[]>()))
            .ReturnsAsync((IJSVoidResult)null!);

        await service.IncrementCountAsync(channel);

        // Act
        await service.ResetCountAsync(channel);

        // Assert
        Assert.Equal(2, eventTriggerCount); // Once for increment, once for reset
    }

    [Fact]
    public async Task ResetCountAsync_WithNonExistentChannel_ShouldNotTriggerEvent()
    {
        // Arrange
        const string channel = "nonexistent";
        var eventTriggered = false;
        service.OnCountsChanged += () => eventTriggered = true;

        moduleMock
            .Setup(x => x.InvokeAsync<IJSVoidResult>(
                "saveUnreadCounts",
                It.IsAny<object[]>()))
            .ReturnsAsync((IJSVoidResult)null!);

        // Act
        await service.ResetCountAsync(channel);

        // Assert
        Assert.False(eventTriggered);
    }

    [Fact]
    public async Task GetCount_WithExistingChannel_ShouldReturnCorrectCount()
    {
        // Arrange
        const string channel = "general";
        moduleMock
            .Setup(x => x.InvokeAsync<IJSVoidResult>(
                "saveUnreadCounts",
                It.IsAny<object[]>()))
            .ReturnsAsync((IJSVoidResult)null!);

        await service.IncrementCountAsync(channel);
        await service.IncrementCountAsync(channel);
        await service.IncrementCountAsync(channel);

        // Act
        var count = service.GetCount(channel);

        // Assert
        Assert.Equal(3, count);
    }

    [Fact]
    public void GetCount_WithNonExistentChannel_ShouldReturn0()
    {
        // Arrange
        const string channel = "nonexistent";

        // Act
        var count = service.GetCount(channel);

        // Assert
        Assert.Equal(0, count);
    }

    [Fact]
    public void GetCount_WithNullOrEmpty_ShouldReturn0()
    {
        // Act & Assert
        Assert.Equal(0, service.GetCount(null!));
        Assert.Equal(0, service.GetCount(string.Empty));
        Assert.Equal(0, service.GetCount("   "));
    }

    [Fact]
    public async Task LoadFromStorageAsync_WithExistingData_ShouldLoadCounts()
    {
        // Arrange
        var storedCounts = new Dictionary<string, int>
        {
            ["general"] = 5,
            ["random"] = 12,
            ["dev"] = 3,
        };

        moduleMock
            .Setup(x => x.InvokeAsync<Dictionary<string, int>>(
                "getUnreadCounts",
                It.IsAny<object[]>()))
            .ReturnsAsync(storedCounts);

        // Act
        await service.LoadFromStorageAsync();

        // Assert
        Assert.Equal(5, service.GetCount("general"));
        Assert.Equal(12, service.GetCount("random"));
        Assert.Equal(3, service.GetCount("dev"));
    }

    [Fact]
    public async Task LoadFromStorageAsync_ShouldTriggerOnCountsChanged()
    {
        // Arrange
        var eventTriggered = false;
        service.OnCountsChanged += () => eventTriggered = true;

        var storedCounts = new Dictionary<string, int>
        {
            ["general"] = 5,
        };

        moduleMock
            .Setup(x => x.InvokeAsync<Dictionary<string, int>>(
                "getUnreadCounts",
                It.IsAny<object[]>()))
            .ReturnsAsync(storedCounts);

        // Act
        await service.LoadFromStorageAsync();

        // Assert
        Assert.True(eventTriggered);
    }

    [Fact]
    public async Task LoadFromStorageAsync_WithEmptyStorage_ShouldNotTriggerEvent()
    {
        // Arrange
        var eventTriggered = false;
        service.OnCountsChanged += () => eventTriggered = true;

        moduleMock
            .Setup(x => x.InvokeAsync<Dictionary<string, int>>(
                "getUnreadCounts",
                It.IsAny<object[]>()))
            .ReturnsAsync([]);

        // Act
        await service.LoadFromStorageAsync();

        // Assert
        Assert.False(eventTriggered);
    }

    [Fact]
    public async Task LoadFromStorageAsync_WithError_ShouldLogWarningAndContinue()
    {
        // Arrange
        moduleMock
            .Setup(x => x.InvokeAsync<Dictionary<string, int>>(
                "getUnreadCounts",
                It.IsAny<object[]>()))
            .ThrowsAsync(new JSException("Storage error"));

        // Act
        await service.LoadFromStorageAsync();

        // Assert
        Assert.Equal(0, service.GetCount("general"));
        loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Erreur lors du chargement des compteurs")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task DisposeAsync_ShouldDisposeModule()
    {
        // Arrange
        moduleMock
            .Setup(x => x.DisposeAsync())
            .Returns(ValueTask.CompletedTask);

        await service.IncrementCountAsync("general");

        // Act
        await service.DisposeAsync();

        // Assert
        moduleMock.Verify(x => x.DisposeAsync(), Times.Once);
    }

    [Fact]
    public async Task DisposeAsync_WithError_ShouldLogWarning()
    {
        // Arrange
        moduleMock
            .Setup(x => x.DisposeAsync())
            .Throws(new JSException("Dispose error"));

        await service.IncrementCountAsync("general");

        // Act
        await service.DisposeAsync();

        // Assert
        loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Erreur lors de la libération")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task MultipleChannels_ShouldMaintainSeparateCounts()
    {
        // Arrange
        moduleMock
            .Setup(x => x.InvokeAsync<IJSVoidResult>(
                "saveUnreadCounts",
                It.IsAny<object[]>()))
            .ReturnsAsync((IJSVoidResult)null!);

        // Act
        await service.IncrementCountAsync("general");
        await service.IncrementCountAsync("general");
        await service.IncrementCountAsync("random");
        await service.IncrementCountAsync("dev");
        await service.IncrementCountAsync("dev");
        await service.IncrementCountAsync("dev");

        // Assert
        Assert.Equal(2, service.GetCount("general"));
        Assert.Equal(1, service.GetCount("random"));
        Assert.Equal(3, service.GetCount("dev"));
    }

    [Fact]
    public async Task IncrementAndReset_ShouldWorkCorrectly()
    {
        // Arrange
        const string channel = "general";
        moduleMock
            .Setup(x => x.InvokeAsync<IJSVoidResult>(
                "saveUnreadCounts",
                It.IsAny<object[]>()))
            .ReturnsAsync((IJSVoidResult)null!);

        // Act
        await service.IncrementCountAsync(channel);
        await service.IncrementCountAsync(channel);
        await service.IncrementCountAsync(channel);

        Assert.Equal(3, service.GetCount(channel));

        await service.ResetCountAsync(channel);

        Assert.Equal(0, service.GetCount(channel));

        await service.IncrementCountAsync(channel);

        // Assert
        Assert.Equal(1, service.GetCount(channel));
    }
}