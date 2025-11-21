using IrcChat.Client.Services;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using Microsoft.JSInterop.Infrastructure;
using Moq;
using Xunit;

namespace IrcChat.Client.Tests.Services;

public class IgnoredUsersServiceTests
{
    private readonly Mock<IJSRuntime> _jsRuntimeMock;
    private readonly Mock<ILogger<IgnoredUsersService>> _loggerMock;
    private readonly Mock<IJSObjectReference> _jsModuleMock;
    private readonly IgnoredUsersService _service;

    public IgnoredUsersServiceTests()
    {
        _jsRuntimeMock = new Mock<IJSRuntime>();
        _loggerMock = new Mock<ILogger<IgnoredUsersService>>();
        _jsModuleMock = new Mock<IJSObjectReference>();

        _jsRuntimeMock
            .Setup(x => x.InvokeAsync<IJSObjectReference>(
                "import", It.IsAny<object[]>()))
            .ReturnsAsync(_jsModuleMock.Object);
        _jsModuleMock
            .Setup(x => x.InvokeAsync<List<string>>(
                "getAllIgnoredUsers", It.IsAny<object[]>()))
            .ReturnsAsync([]);

        _service = new IgnoredUsersService(_jsRuntimeMock.Object, _loggerMock.Object);
    }

    [Fact]
    public async Task InitializeAsync_ShouldLoadModule()
    {
        // Act
        await _service.InitializeAsync();

        // Assert
        _jsRuntimeMock.Verify(
            x => x.InvokeAsync<IJSObjectReference>("import", It.IsAny<object[]>()),
            Times.Once);
        _jsModuleMock.Verify(
            x => x.InvokeAsync<List<string>>("getAllIgnoredUsers", It.IsAny<object[]>()),
            Times.Once);
    }

    [Fact]
    public async Task IsUserIgnored_WhenModuleNotLoaded_ShouldReturnFalse()
    {
        // Act
        var result = _service.IsUserIgnored(Guid.NewGuid().ToString());

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task IgnoreUserAsync_WhenModuleLoaded_ShouldCallJSFunctionAndInvokeEvent()
    {
        // Arrange
        var userId = Guid.NewGuid().ToString();
        var eventInvoked = false;
        await _service.InitializeAsync();

        _jsModuleMock
            .Setup(x => x.InvokeAsync<IJSVoidResult>("ignoreUser", It.IsAny<object[]>()))
            .ReturnsAsync((IJSVoidResult)null!);

        _service.OnIgnoredUsersChanged += () => eventInvoked = true;

        // Act
        await _service.IgnoreUserAsync(userId);

        // Assert
        Assert.True(eventInvoked);
        _jsModuleMock.Verify(
            x => x.InvokeAsync<IJSVoidResult>("ignoreUser", It.Is<object[]>(args => args[0].ToString() == userId.ToString())),
            Times.Once);
    }

    [Fact]
    public async Task UnignoreUserAsync_WhenModuleLoaded_ShouldCallJSFunctionAndInvokeEvent()
    {
        // Arrange
        var userId = Guid.NewGuid().ToString();
        var eventInvoked = false;
        await _service.InitializeAsync();

        _jsModuleMock
            .Setup(x => x.InvokeAsync<IJSVoidResult>("unignoreUser", It.IsAny<object[]>()))
            .ReturnsAsync((IJSVoidResult)null!);

        _service.OnIgnoredUsersChanged += () => eventInvoked = true;

        // Act
        await _service.UnignoreUserAsync(userId);

        // Assert
        Assert.True(eventInvoked);
        _jsModuleMock.Verify(
            x => x.InvokeAsync<IJSVoidResult>("unignoreUser", It.Is<object[]>(args => args[0].ToString() == userId.ToString())),
            Times.Once);
    }

    [Fact]
    public async Task ToggleIgnoreUserAsync_WhenUserNotIgnored_ShouldIgnore()
    {
        // Arrange
        var userId = Guid.NewGuid().ToString();
        await _service.InitializeAsync();

        _jsModuleMock
            .Setup(x => x.InvokeAsync<IJSVoidResult>("ignoreUser", It.IsAny<object[]>()))
            .ReturnsAsync((IJSVoidResult)null!);

        _service.OnIgnoredUsersChanged += () => { };

        // Act
        await _service.ToggleIgnoreUserAsync(userId);

        // Assert
        _jsModuleMock.Verify(
            x => x.InvokeAsync<IJSVoidResult>("ignoreUser", It.IsAny<object[]>()),
            Times.Once);
    }

    [Fact]
    public async Task ToggleIgnoreUserAsync_WhenUserIgnored_ShouldUnignore()
    {
        // Arrange
        var userId = Guid.NewGuid().ToString();
        await _service.InitializeAsync();

        _jsModuleMock
            .Setup(x => x.InvokeAsync<IJSVoidResult>("ignoreUser", It.IsAny<object[]>()))
            .ReturnsAsync((IJSVoidResult)null!);
        _jsModuleMock
            .Setup(x => x.InvokeAsync<IJSVoidResult>("unignoreUser", It.IsAny<object[]>()))
            .ReturnsAsync((IJSVoidResult)null!);

        _service.OnIgnoredUsersChanged += () => { };

        // Act
        await _service.ToggleIgnoreUserAsync(userId);
        await _service.ToggleIgnoreUserAsync(userId);

        // Assert
        _jsModuleMock.Verify(
            x => x.InvokeAsync<IJSVoidResult>("unignoreUser", It.IsAny<object[]>()),
            Times.Once);
    }

    [Fact]
    public async Task OnIgnoredUsersChanged_ShouldBeInvokedWhenIgnoringUser()
    {
        // Arrange
        var userId = Guid.NewGuid().ToString();
        var eventCount = 0;
        await _service.InitializeAsync();

        _jsModuleMock
            .Setup(x => x.InvokeAsync<IJSVoidResult>("ignoreUser", It.IsAny<object[]>()))
            .ReturnsAsync((IJSVoidResult)null!);

        _service.OnIgnoredUsersChanged += () => eventCount++;

        // Act
        await _service.IgnoreUserAsync(userId);

        // Assert
        Assert.Equal(1, eventCount);
    }

    [Fact]
    public async Task OnIgnoredUsersChanged_ShouldBeInvokedWhenUnignoringUser()
    {
        // Arrange
        var userId = Guid.NewGuid().ToString();
        var eventCount = 0;
        await _service.InitializeAsync();

        _jsModuleMock
            .Setup(x => x.InvokeAsync<IJSVoidResult>("unignoreUser", It.IsAny<object[]>()))
            .ReturnsAsync((IJSVoidResult)null!);

        _service.OnIgnoredUsersChanged += () => eventCount++;

        // Act
        await _service.UnignoreUserAsync(userId);

        // Assert
        Assert.Equal(1, eventCount);
    }

    [Fact]
    public async Task DisposeAsync_ShouldDisposeModule()
    {
        // Arrange
        await _service.InitializeAsync();

        _jsModuleMock
            .Setup(x => x.DisposeAsync())
            .Returns(ValueTask.CompletedTask);

        // Act
        await _service.DisposeAsync();

        // Assert
        _jsModuleMock.Verify(x => x.DisposeAsync(), Times.Once);
    }
}