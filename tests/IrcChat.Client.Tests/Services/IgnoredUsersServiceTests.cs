using IrcChat.Client.Services;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace IrcChat.Client.Tests.Services;

public class IgnoredUsersServiceTests
{
    private readonly Mock<IJSRuntime> jsRuntimeMock;
    private readonly Mock<ILogger<IgnoredUsersService>> loggerMock;
    private readonly Mock<IJSObjectReference> jsModuleMock;
    private readonly IgnoredUsersService service;

    public IgnoredUsersServiceTests()
    {
        jsRuntimeMock = new Mock<IJSRuntime>();
        loggerMock = new Mock<ILogger<IgnoredUsersService>>();
        jsModuleMock = new Mock<IJSObjectReference>();

        jsRuntimeMock
            .Setup(x => x.InvokeAsync<IJSObjectReference>(
                "import", It.IsAny<object[]>()))
            .ReturnsAsync(jsModuleMock.Object);
        jsModuleMock
            .Setup(x => x.InvokeAsync<List<string>>(
                "getAllIgnoredUsers", It.IsAny<object[]>()))
            .ReturnsAsync([]);

        service = new IgnoredUsersService(jsRuntimeMock.Object, loggerMock.Object);
    }

    [Fact]
    public async Task InitializeAsync_ShouldLoadModule()
    {
        // Act
        await service.InitializeAsync();

        // Assert
        jsRuntimeMock.Verify(
            x => x.InvokeAsync<IJSObjectReference>("import", It.IsAny<object[]>()),
            Times.Once);
        jsModuleMock.Verify(
            x => x.InvokeAsync<List<string>>("getAllIgnoredUsers", It.IsAny<object[]>()),
            Times.Once);
    }

    [Fact]
    public async Task IsUserIgnored_WhenModuleNotLoaded_ShouldReturnFalse()
    {
        // Act
        var result = service.IsUserIgnored(Guid.NewGuid().ToString());

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task IgnoreUserAsync_WhenModuleLoaded_ShouldCallJSFunctionAndInvokeEvent()
    {
        // Arrange
        var userId = Guid.NewGuid().ToString();
        var eventInvoked = false;
        await service.InitializeAsync();

        jsModuleMock
            .Setup(x => x.InvokeAsync<IJSVoidResult>("ignoreUser", It.IsAny<object[]>()))
            .ReturnsAsync((IJSVoidResult)null!);

        service.OnIgnoredUsersChanged += () => eventInvoked = true;

        // Act
        await service.IgnoreUserAsync(userId);

        // Assert
        Assert.True(eventInvoked);
        jsModuleMock.Verify(
            x => x.InvokeAsync<IJSVoidResult>("ignoreUser", It.Is<object[]>(args => args[0].ToString() == userId.ToString())),
            Times.Once);
    }

    [Fact]
    public async Task UnignoreUserAsync_WhenModuleLoaded_ShouldCallJSFunctionAndInvokeEvent()
    {
        // Arrange
        var userId = Guid.NewGuid().ToString();
        var eventInvoked = false;
        await service.InitializeAsync();

        jsModuleMock
            .Setup(x => x.InvokeAsync<IJSVoidResult>("unignoreUser", It.IsAny<object[]>()))
            .ReturnsAsync((IJSVoidResult)null!);

        service.OnIgnoredUsersChanged += () => eventInvoked = true;

        // Act
        await service.UnignoreUserAsync(userId);

        // Assert
        Assert.True(eventInvoked);
        jsModuleMock.Verify(
            x => x.InvokeAsync<IJSVoidResult>("unignoreUser", It.Is<object[]>(args => args[0].ToString() == userId.ToString())),
            Times.Once);
    }

    [Fact]
    public async Task ToggleIgnoreUserAsync_WhenUserNotIgnored_ShouldIgnore()
    {
        // Arrange
        var userId = Guid.NewGuid().ToString();
        await service.InitializeAsync();

        jsModuleMock
            .Setup(x => x.InvokeAsync<IJSVoidResult>("ignoreUser", It.IsAny<object[]>()))
            .ReturnsAsync((IJSVoidResult)null!);

        service.OnIgnoredUsersChanged += () => { };

        // Act
        await service.ToggleIgnoreUserAsync(userId);

        // Assert
        jsModuleMock.Verify(
            x => x.InvokeAsync<IJSVoidResult>("ignoreUser", It.IsAny<object[]>()),
            Times.Once);
    }

    [Fact]
    public async Task ToggleIgnoreUserAsync_WhenUserIgnored_ShouldUnignore()
    {
        // Arrange
        var userId = Guid.NewGuid().ToString();
        await service.InitializeAsync();

        jsModuleMock
            .Setup(x => x.InvokeAsync<IJSVoidResult>("ignoreUser", It.IsAny<object[]>()))
            .ReturnsAsync((IJSVoidResult)null!);
        jsModuleMock
            .Setup(x => x.InvokeAsync<IJSVoidResult>("unignoreUser", It.IsAny<object[]>()))
            .ReturnsAsync((IJSVoidResult)null!);

        service.OnIgnoredUsersChanged += () => { };

        // Act
        await service.ToggleIgnoreUserAsync(userId);
        await service.ToggleIgnoreUserAsync(userId);

        // Assert
        jsModuleMock.Verify(
            x => x.InvokeAsync<IJSVoidResult>("unignoreUser", It.IsAny<object[]>()),
            Times.Once);
    }

    [Fact]
    public async Task OnIgnoredUsersChanged_ShouldBeInvokedWhenIgnoringUser()
    {
        // Arrange
        var userId = Guid.NewGuid().ToString();
        var eventCount = 0;
        await service.InitializeAsync();

        jsModuleMock
            .Setup(x => x.InvokeAsync<IJSVoidResult>("ignoreUser", It.IsAny<object[]>()))
            .ReturnsAsync((IJSVoidResult)null!);

        service.OnIgnoredUsersChanged += () => eventCount++;

        // Act
        await service.IgnoreUserAsync(userId);

        // Assert
        Assert.Equal(1, eventCount);
    }

    [Fact]
    public async Task OnIgnoredUsersChanged_ShouldBeInvokedWhenUnignoringUser()
    {
        // Arrange
        var userId = Guid.NewGuid().ToString();
        var eventCount = 0;
        await service.InitializeAsync();

        jsModuleMock
            .Setup(x => x.InvokeAsync<IJSVoidResult>("unignoreUser", It.IsAny<object[]>()))
            .ReturnsAsync((IJSVoidResult)null!);

        service.OnIgnoredUsersChanged += () => eventCount++;

        // Act
        await service.UnignoreUserAsync(userId);

        // Assert
        Assert.Equal(1, eventCount);
    }

    [Fact]
    public async Task DisposeAsync_ShouldDisposeModule()
    {
        // Arrange
        await service.InitializeAsync();

        jsModuleMock
            .Setup(x => x.DisposeAsync())
            .Returns(ValueTask.CompletedTask);

        // Act
        await service.DisposeAsync();

        // Assert
        jsModuleMock.Verify(x => x.DisposeAsync(), Times.Once);
    }
}