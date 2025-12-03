// tests/IrcChat.Client.Tests/Services/ActiveChannelsServiceTests.cs

using System.Text.Json;
using IrcChat.Client.Services;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using Microsoft.JSInterop.Infrastructure;
using Moq;
using Xunit;

namespace IrcChat.Client.Tests.Services;

public sealed class ActiveChannelsServiceTests
{
    private readonly Mock<IJSRuntime> _jsRuntimeMock;
    private readonly Mock<ILogger<ActiveChannelsService>> _loggerMock;
    private readonly ActiveChannelsService _service;

    public ActiveChannelsServiceTests()
    {
        _jsRuntimeMock = new Mock<IJSRuntime>();
        _loggerMock = new Mock<ILogger<ActiveChannelsService>>();
        _service = new ActiveChannelsService(_jsRuntimeMock.Object, _loggerMock.Object);
    }

    [Fact]
    public async Task InitializeAsync_WhenNoDataInLocalStorage_ShouldInitializeEmptyList()
    {
        // Arrange
        _jsRuntimeMock
            .Setup(x => x.InvokeAsync<string?>("localStorage.getItem", It.IsAny<object[]>()))
            .ReturnsAsync((string?)null);

        // Act
        await _service.InitializeAsync();
        var channels = await _service.GetActiveChannelsAsync();

        // Assert
        Assert.Empty(channels);
    }

    [Fact]
    public async Task InitializeAsync_WhenDataExists_ShouldLoadFromLocalStorage()
    {
        // Arrange
        var savedChannels = new List<string> { "general", "random", "support" };
        var json = JsonSerializer.Serialize(savedChannels);

        _jsRuntimeMock
            .Setup(x => x.InvokeAsync<string?>("localStorage.getItem", It.IsAny<object[]>()))
            .ReturnsAsync(json);

        // Act
        await _service.InitializeAsync();
        var channels = await _service.GetActiveChannelsAsync();

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
        _jsRuntimeMock
            .Setup(x => x.InvokeAsync<string?>("localStorage.getItem", It.IsAny<object[]>()))
            .ReturnsAsync((string?)null);

        // Act
        await _service.InitializeAsync();
        await _service.InitializeAsync();
        await _service.InitializeAsync();

        // Assert
        _jsRuntimeMock.Verify(
            x => x.InvokeAsync<string?>("localStorage.getItem", It.IsAny<object[]>()),
            Times.Once);
    }

    [Fact]
    public async Task AddChannelAsync_NewChannel_ShouldAddToList()
    {
        // Arrange
        _jsRuntimeMock
            .Setup(x => x.InvokeAsync<string?>("localStorage.getItem", It.IsAny<object[]>()))
            .ReturnsAsync((string?)null);

        _jsRuntimeMock
            .Setup(x => x.InvokeAsync<IJSVoidResult>("localStorage.setItem", It.IsAny<object[]>()))
            .ReturnsAsync((IJSVoidResult)null!);

        // Act
        await _service.AddChannelAsync("general");
        var channels = await _service.GetActiveChannelsAsync();

        // Assert
        Assert.Single(channels);
        Assert.Contains("general", channels);
    }

    [Fact]
    public async Task AddChannelAsync_DuplicateChannel_ShouldNotAddTwice()
    {
        // Arrange
        _jsRuntimeMock
            .Setup(x => x.InvokeAsync<string?>("localStorage.getItem", It.IsAny<object[]>()))
            .ReturnsAsync((string?)null);

        _jsRuntimeMock
            .Setup(x => x.InvokeAsync<IJSVoidResult>("localStorage.setItem", It.IsAny<object[]>()))
            .ReturnsAsync((IJSVoidResult)null!);

        // Act
        await _service.AddChannelAsync("general");
        await _service.AddChannelAsync("general");
        await _service.AddChannelAsync("GENERAL");

        var channels = await _service.GetActiveChannelsAsync();

        // Assert
        Assert.Single(channels);
    }

    [Fact]
    public async Task AddChannelAsync_WithWhitespace_ShouldTrim()
    {
        // Arrange
        _jsRuntimeMock
            .Setup(x => x.InvokeAsync<string?>("localStorage.getItem", It.IsAny<object[]>()))
            .ReturnsAsync((string?)null);

        _jsRuntimeMock
            .Setup(x => x.InvokeAsync<IJSVoidResult>("localStorage.setItem", It.IsAny<object[]>()))
            .ReturnsAsync((IJSVoidResult)null!);

        // Act
        await _service.AddChannelAsync("  general  ");
        var channels = await _service.GetActiveChannelsAsync();

        // Assert
        Assert.Single(channels);
        Assert.Equal("general", channels[0]);
    }

    [Fact]
    public async Task AddChannelAsync_EmptyString_ShouldNotAdd()
    {
        // Arrange
        _jsRuntimeMock
            .Setup(x => x.InvokeAsync<string?>("localStorage.getItem", It.IsAny<object[]>()))
            .ReturnsAsync((string?)null);

        // Act
        await _service.AddChannelAsync("");
        await _service.AddChannelAsync("   ");

        var channels = await _service.GetActiveChannelsAsync();

        // Assert
        Assert.Empty(channels);
    }

    [Fact]
    public async Task AddChannelAsync_ShouldSaveToLocalStorage()
    {
        // Arrange
        _jsRuntimeMock
            .Setup(x => x.InvokeAsync<string?>("localStorage.getItem", It.IsAny<object[]>()))
            .ReturnsAsync((string?)null);

        _jsRuntimeMock
            .Setup(x => x.InvokeAsync<IJSVoidResult>("localStorage.setItem", It.IsAny<object[]>()))
            .ReturnsAsync((IJSVoidResult)null!);

        // Act
        await _service.AddChannelAsync("general");

        // Assert
        _jsRuntimeMock.Verify(
            x => x.InvokeAsync<IJSVoidResult>("localStorage.setItem", It.IsAny<object[]>()),
            Times.Once);
    }

    [Fact]
    public async Task RemoveChannelAsync_ExistingChannel_ShouldRemoveFromList()
    {
        // Arrange
        var savedChannels = new List<string> { "general", "random", "support" };
        var json = JsonSerializer.Serialize(savedChannels);

        _jsRuntimeMock
            .Setup(x => x.InvokeAsync<string?>("localStorage.getItem", It.IsAny<object[]>()))
            .ReturnsAsync(json);

        _jsRuntimeMock
            .Setup(x => x.InvokeAsync<IJSVoidResult>("localStorage.setItem", It.IsAny<object[]>()))
            .ReturnsAsync((IJSVoidResult)null!);

        // Act
        await _service.RemoveChannelAsync("random");
        var channels = await _service.GetActiveChannelsAsync();

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

        _jsRuntimeMock
            .Setup(x => x.InvokeAsync<string?>("localStorage.getItem", It.IsAny<object[]>()))
            .ReturnsAsync(json);

        _jsRuntimeMock
            .Setup(x => x.InvokeAsync<IJSVoidResult>("localStorage.setItem", It.IsAny<object[]>()))
            .ReturnsAsync((IJSVoidResult)null!);

        await _service.InitializeAsync();

        // Act
        await _service.RemoveChannelAsync("nonexisting");
        var channels = await _service.GetActiveChannelsAsync();

        // Assert
        Assert.Single(channels);
        Assert.Equal("general", channels[0]);

        // Vérifier qu'on n'a pas sauvegardé inutilement
        _jsRuntimeMock.Verify(
            x => x.InvokeAsync<IJSVoidResult>("localStorage.setItem", It.IsAny<object[]>()),
            Times.Never);
    }

    [Fact]
    public async Task RemoveChannelAsync_CaseInsensitive_ShouldRemove()
    {
        // Arrange
        var savedChannels = new List<string> { "General", "Random" };
        var json = JsonSerializer.Serialize(savedChannels);

        _jsRuntimeMock
            .Setup(x => x.InvokeAsync<string?>("localStorage.getItem", It.IsAny<object[]>()))
            .ReturnsAsync(json);

        _jsRuntimeMock
            .Setup(x => x.InvokeAsync<IJSVoidResult>("localStorage.setItem", It.IsAny<object[]>()))
            .ReturnsAsync((IJSVoidResult)null!);

        // Act
        await _service.RemoveChannelAsync("GENERAL");
        var channels = await _service.GetActiveChannelsAsync();

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

        _jsRuntimeMock
            .Setup(x => x.InvokeAsync<string?>("localStorage.getItem", It.IsAny<object[]>()))
            .ReturnsAsync(json);

        _jsRuntimeMock
            .Setup(x => x.InvokeAsync<IJSVoidResult>("localStorage.setItem", It.IsAny<object[]>()))
            .ReturnsAsync((IJSVoidResult)null!);

        await _service.InitializeAsync();

        // Act
        await _service.ClearAsync();
        var channels = await _service.GetActiveChannelsAsync();

        // Assert
        Assert.Empty(channels);
    }

    [Fact]
    public async Task GetActiveChannelsAsync_ShouldReturnCopyOfList()
    {
        // Arrange
        _jsRuntimeMock
            .Setup(x => x.InvokeAsync<string?>("localStorage.getItem", It.IsAny<object[]>()))
            .ReturnsAsync((string?)null);

        _jsRuntimeMock
            .Setup(x => x.InvokeAsync<IJSVoidResult>("localStorage.setItem", It.IsAny<object[]>()))
            .ReturnsAsync((IJSVoidResult)null!);

        await _service.AddChannelAsync("general");

        // Act
        var channels1 = await _service.GetActiveChannelsAsync();
        channels1.Add("hacked");
        var channels2 = await _service.GetActiveChannelsAsync();

        // Assert
        Assert.Single(channels2);
        Assert.DoesNotContain("hacked", channels2);
    }

    [Fact]
    public async Task InitializeAsync_WithInvalidJson_ShouldInitializeEmptyList()
    {
        // Arrange
        _jsRuntimeMock
            .Setup(x => x.InvokeAsync<string?>("localStorage.getItem", It.IsAny<object[]>()))
            .ReturnsAsync("invalid json {{{");

        // Act
        await _service.InitializeAsync();
        var channels = await _service.GetActiveChannelsAsync();

        // Assert
        Assert.Empty(channels);
    }

    [Fact]
    public async Task AddChannelAsync_MultipleChannels_ShouldMaintainOrder()
    {
        // Arrange
        _jsRuntimeMock
            .Setup(x => x.InvokeAsync<string?>("localStorage.getItem", It.IsAny<object[]>()))
            .ReturnsAsync((string?)null);

        _jsRuntimeMock
            .Setup(x => x.InvokeAsync<IJSVoidResult>("localStorage.setItem", It.IsAny<object[]>()))
            .ReturnsAsync((IJSVoidResult)null!);

        // Act
        await _service.AddChannelAsync("general");
        await _service.AddChannelAsync("random");
        await _service.AddChannelAsync("support");

        var channels = await _service.GetActiveChannelsAsync();

        // Assert
        Assert.Equal(3, channels.Count);
        Assert.Equal("general", channels[0]);
        Assert.Equal("random", channels[1]);
        Assert.Equal("support", channels[2]);
    }
}