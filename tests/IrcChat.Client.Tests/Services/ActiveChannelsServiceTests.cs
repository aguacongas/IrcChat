// tests/IrcChat.Client.Tests/Services/ActiveChannelsServiceTests.cs

using System.Text.Json;
using IrcChat.Client.Services;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace IrcChat.Client.Tests.Services;

public sealed class ActiveChannelsServiceTests
{
    private readonly Mock<IJSRuntime> jsRuntimeMock;
    private readonly Mock<ILogger<ActiveChannelsService>> loggerMock;
    private readonly ActiveChannelsService service;

    public ActiveChannelsServiceTests()
    {
        jsRuntimeMock = new Mock<IJSRuntime>();
        loggerMock = new Mock<ILogger<ActiveChannelsService>>();
        service = new ActiveChannelsService(jsRuntimeMock.Object, loggerMock.Object);
    }

    [Fact]
    public async Task InitializeAsync_WhenNoDataInLocalStorage_ShouldInitializeEmptyList()
    {
        // Arrange
        jsRuntimeMock
            .Setup(x => x.InvokeAsync<string?>("localStorage.getItem", It.IsAny<object[]>()))
            .ReturnsAsync((string?)null);

        // Act
        await service.InitializeAsync();
        var channels = await service.GetActiveChannelsAsync();

        // Assert
        Assert.Empty(channels);
    }

    [Fact]
    public async Task InitializeAsync_WhenDataExists_ShouldLoadFromLocalStorage()
    {
        // Arrange
        var savedChannels = new List<string> { "general", "random", "support" };
        var json = JsonSerializer.Serialize(savedChannels);

        jsRuntimeMock
            .Setup(x => x.InvokeAsync<string?>("localStorage.getItem", It.IsAny<object[]>()))
            .ReturnsAsync(json);

        // Act
        await service.InitializeAsync();
        var channels = await service.GetActiveChannelsAsync();

        // Assert
        Assert.Equal(3, channels.Count);
        Assert.Contains("general", channels);
        Assert.Contains("random", channels);
        Assert.Contains("support", channels);
    }

    [Fact]
    public async Task InitializeAsync_CalledMultipleTimes_ShouldInitializeOnlyOnce()
    {
        // Arrange
        jsRuntimeMock
            .Setup(x => x.InvokeAsync<string?>("localStorage.getItem", It.IsAny<object[]>()))
            .ReturnsAsync((string?)null);

        // Act
        await service.InitializeAsync();
        await service.InitializeAsync();
        await service.InitializeAsync();

        // Assert
        jsRuntimeMock.Verify(
            x => x.InvokeAsync<string?>("localStorage.getItem", It.IsAny<object[]>()),
            Times.Once);
    }

    [Fact]
    public async Task AddChannelAsync_NewChannel_ShouldAddToList()
    {
        // Arrange
        jsRuntimeMock
            .Setup(x => x.InvokeAsync<string?>("localStorage.getItem", It.IsAny<object[]>()))
            .ReturnsAsync((string?)null);

        jsRuntimeMock
            .Setup(x => x.InvokeAsync<IJSVoidResult>("localStorage.setItem", It.IsAny<object[]>()))
            .ReturnsAsync((IJSVoidResult)null!);

        // Act
        await service.AddChannelAsync("general");
        var channels = await service.GetActiveChannelsAsync();

        // Assert
        Assert.Single(channels);
        Assert.Contains("general", channels);
    }

    [Fact]
    public async Task AddChannelAsync_DuplicateChannel_ShouldNotAddTwice()
    {
        // Arrange
        jsRuntimeMock
            .Setup(x => x.InvokeAsync<string?>("localStorage.getItem", It.IsAny<object[]>()))
            .ReturnsAsync((string?)null);

        jsRuntimeMock
            .Setup(x => x.InvokeAsync<IJSVoidResult>("localStorage.setItem", It.IsAny<object[]>()))
            .ReturnsAsync((IJSVoidResult)null!);

        // Act
        await service.AddChannelAsync("general");
        await service.AddChannelAsync("general");
        await service.AddChannelAsync("GENERAL");

        var channels = await service.GetActiveChannelsAsync();

        // Assert
        Assert.Single(channels);
    }

    [Fact]
    public async Task AddChannelAsync_WithWhitespace_ShouldTrim()
    {
        // Arrange
        jsRuntimeMock
            .Setup(x => x.InvokeAsync<string?>("localStorage.getItem", It.IsAny<object[]>()))
            .ReturnsAsync((string?)null);

        jsRuntimeMock
            .Setup(x => x.InvokeAsync<IJSVoidResult>("localStorage.setItem", It.IsAny<object[]>()))
            .ReturnsAsync((IJSVoidResult)null!);

        // Act
        await service.AddChannelAsync("  general  ");
        var channels = await service.GetActiveChannelsAsync();

        // Assert
        Assert.Single(channels);
        Assert.Equal("general", channels[0]);
    }

    [Fact]
    public async Task AddChannelAsync_EmptyString_ShouldNotAdd()
    {
        // Arrange
        jsRuntimeMock
            .Setup(x => x.InvokeAsync<string?>("localStorage.getItem", It.IsAny<object[]>()))
            .ReturnsAsync((string?)null);

        // Act
        await service.AddChannelAsync(string.Empty);
        await service.AddChannelAsync("   ");

        var channels = await service.GetActiveChannelsAsync();

        // Assert
        Assert.Empty(channels);
    }

    [Fact]
    public async Task AddChannelAsync_ShouldSaveToLocalStorage()
    {
        // Arrange
        jsRuntimeMock
            .Setup(x => x.InvokeAsync<string?>("localStorage.getItem", It.IsAny<object[]>()))
            .ReturnsAsync((string?)null);

        jsRuntimeMock
            .Setup(x => x.InvokeAsync<IJSVoidResult>("localStorage.setItem", It.IsAny<object[]>()))
            .ReturnsAsync((IJSVoidResult)null!);

        // Act
        await service.AddChannelAsync("general");

        // Assert
        jsRuntimeMock.Verify(
            x => x.InvokeAsync<IJSVoidResult>("localStorage.setItem", It.IsAny<object[]>()),
            Times.Once);
    }

    [Fact]
    public async Task RemoveChannelAsync_ExistingChannel_ShouldRemoveFromList()
    {
        // Arrange
        var savedChannels = new List<string> { "general", "random", "support" };
        var json = JsonSerializer.Serialize(savedChannels);

        jsRuntimeMock
            .Setup(x => x.InvokeAsync<string?>("localStorage.getItem", It.IsAny<object[]>()))
            .ReturnsAsync(json);

        jsRuntimeMock
            .Setup(x => x.InvokeAsync<IJSVoidResult>("localStorage.setItem", It.IsAny<object[]>()))
            .ReturnsAsync((IJSVoidResult)null!);

        // Act
        await service.RemoveChannelAsync("random");
        var channels = await service.GetActiveChannelsAsync();

        // Assert
        Assert.Equal(2, channels.Count);
        Assert.DoesNotContain("random", channels);
        Assert.Contains("general", channels);
        Assert.Contains("support", channels);
    }

    [Fact]
    public async Task RemoveChannelAsync_NonExistingChannel_ShouldDoNothing()
    {
        // Arrange
        var savedChannels = new List<string> { "general" };
        var json = JsonSerializer.Serialize(savedChannels);

        jsRuntimeMock
            .Setup(x => x.InvokeAsync<string?>("localStorage.getItem", It.IsAny<object[]>()))
            .ReturnsAsync(json);

        jsRuntimeMock
            .Setup(x => x.InvokeAsync<IJSVoidResult>("localStorage.setItem", It.IsAny<object[]>()))
            .ReturnsAsync((IJSVoidResult)null!);

        await service.InitializeAsync();

        // Act
        await service.RemoveChannelAsync("nonexisting");
        var channels = await service.GetActiveChannelsAsync();

        // Assert
        Assert.Single(channels);
        Assert.Equal("general", channels[0]);

        // Vérifier qu'on n'a pas sauvegardé inutilement
        jsRuntimeMock.Verify(
            x => x.InvokeAsync<IJSVoidResult>("localStorage.setItem", It.IsAny<object[]>()),
            Times.Never);
    }

    [Fact]
    public async Task RemoveChannelAsync_CaseInsensitive_ShouldRemove()
    {
        // Arrange
        var savedChannels = new List<string> { "General", "Random" };
        var json = JsonSerializer.Serialize(savedChannels);

        jsRuntimeMock
            .Setup(x => x.InvokeAsync<string?>("localStorage.getItem", It.IsAny<object[]>()))
            .ReturnsAsync(json);

        jsRuntimeMock
            .Setup(x => x.InvokeAsync<IJSVoidResult>("localStorage.setItem", It.IsAny<object[]>()))
            .ReturnsAsync((IJSVoidResult)null!);

        // Act
        await service.RemoveChannelAsync("GENERAL");
        var channels = await service.GetActiveChannelsAsync();

        // Assert
        Assert.Single(channels);
        Assert.Equal("Random", channels[0]);
    }

    [Fact]
    public async Task ClearAsync_ShouldRemoveAllChannels()
    {
        // Arrange
        var savedChannels = new List<string> { "general", "random", "support" };
        var json = JsonSerializer.Serialize(savedChannels);

        jsRuntimeMock
            .Setup(x => x.InvokeAsync<string?>("localStorage.getItem", It.IsAny<object[]>()))
            .ReturnsAsync(json);

        jsRuntimeMock
            .Setup(x => x.InvokeAsync<IJSVoidResult>("localStorage.setItem", It.IsAny<object[]>()))
            .ReturnsAsync((IJSVoidResult)null!);

        await service.InitializeAsync();

        // Act
        await service.ClearAsync();
        var channels = await service.GetActiveChannelsAsync();

        // Assert
        Assert.Empty(channels);
    }

    [Fact]
    public async Task GetActiveChannelsAsync_ShouldReturnCopyOfList()
    {
        // Arrange
        jsRuntimeMock
            .Setup(x => x.InvokeAsync<string?>("localStorage.getItem", It.IsAny<object[]>()))
            .ReturnsAsync((string?)null);

        jsRuntimeMock
            .Setup(x => x.InvokeAsync<IJSVoidResult>("localStorage.setItem", It.IsAny<object[]>()))
            .ReturnsAsync((IJSVoidResult)null!);

        await service.AddChannelAsync("general");

        // Act
        var channels1 = await service.GetActiveChannelsAsync();
        channels1.Add("hacked");
        var channels2 = await service.GetActiveChannelsAsync();

        // Assert
        Assert.Single(channels2);
        Assert.DoesNotContain("hacked", channels2);
    }

    [Fact]
    public async Task InitializeAsync_WithInvalidJson_ShouldInitializeEmptyList()
    {
        // Arrange
        jsRuntimeMock
            .Setup(x => x.InvokeAsync<string?>("localStorage.getItem", It.IsAny<object[]>()))
            .ReturnsAsync("invalid json {{{");

        // Act
        await service.InitializeAsync();
        var channels = await service.GetActiveChannelsAsync();

        // Assert
        Assert.Empty(channels);
    }

    [Fact]
    public async Task AddChannelAsync_MultipleChannels_ShouldMaintainOrder()
    {
        // Arrange
        jsRuntimeMock
            .Setup(x => x.InvokeAsync<string?>("localStorage.getItem", It.IsAny<object[]>()))
            .ReturnsAsync((string?)null);

        jsRuntimeMock
            .Setup(x => x.InvokeAsync<IJSVoidResult>("localStorage.setItem", It.IsAny<object[]>()))
            .ReturnsAsync((IJSVoidResult)null!);

        // Act
        await service.AddChannelAsync("general");
        await service.AddChannelAsync("random");
        await service.AddChannelAsync("support");

        var channels = await service.GetActiveChannelsAsync();

        // Assert
        Assert.Equal(3, channels.Count);
        Assert.Equal("general", channels[0]);
        Assert.Equal("random", channels[1]);
        Assert.Equal("support", channels[2]);
    }
}