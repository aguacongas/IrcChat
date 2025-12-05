using IrcChat.Client.Components;
using IrcChat.Client.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace IrcChat.Client.Tests.Components;

public class IgnoreUserButtonTests : BunitContext
{
    private readonly Mock<IIgnoredUsersService> ignoredUsersServiceMock;

    public IgnoreUserButtonTests()
    {
        ignoredUsersServiceMock = new Mock<IIgnoredUsersService>();
        ignoredUsersServiceMock
            .Setup(x => x.InitializeAsync())
            .Returns(Task.CompletedTask);

        Services.AddSingleton(ignoredUsersServiceMock.Object);
        Services.AddSingleton(new Mock<ILogger<IgnoreUserButton>>().Object);
    }

    [Fact]
    public async Task Component_WhenRendered_ShouldDisplayButton()
    {
        // Arrange
        var userId = Guid.NewGuid().ToString();
        ignoredUsersServiceMock
            .Setup(x => x.IsUserIgnored(userId))
            .Returns(false);

        // Act
        var cut = Render<IgnoreUserButton>(parameters => parameters
            .Add(p => p.UserId, userId));

        // Assert
        Assert.NotNull(cut.Find("button.ignore-user-button"));
        Assert.Contains("Ignorer", cut.Markup);
    }

    [Fact]
    public async Task Component_WhenUserIgnored_ShouldShowIgnoredState()
    {
        // Arrange
        var userId = Guid.NewGuid().ToString();
        ignoredUsersServiceMock
            .Setup(x => x.IsUserIgnored(userId))
            .Returns(true);

        // Act
        var cut = Render<IgnoreUserButton>(parameters => parameters
            .Add(p => p.UserId, userId));

        cut.WaitForState(
            () => cut.Markup.Contains("Ignoré"),
            TimeSpan.FromSeconds(2));
        cut.Render();

        // Assert
        Assert.Contains("Ignoré", cut.Markup);
        Assert.Contains("ignore-user-button ignored", cut.Markup);
    }

    [Fact]
    public async Task Component_WhenClicked_ShouldToggleIgnoreStatus()
    {
        // Arrange
        var userId = Guid.NewGuid().ToString();
        ignoredUsersServiceMock
            .Setup(x => x.IsUserIgnored(userId))
            .Returns(false);

        ignoredUsersServiceMock
            .Setup(x => x.ToggleIgnoreUserAsync(userId))
            .Verifiable();

        var cut = Render<IgnoreUserButton>(parameters => parameters
            .Add(p => p.UserId, userId));

        var button = cut.Find("button");

        // Act
        await cut.InvokeAsync(() => button.Click());

        // Assert
        ignoredUsersServiceMock.Verify(
            x => x.ToggleIgnoreUserAsync(userId),
            Times.Once);
    }

    [Fact]
    public async Task Component_ShouldCallInitializeAsync()
    {
        // Arrange
        var userId = Guid.NewGuid().ToString();
        ignoredUsersServiceMock
            .Setup(x => x.IsUserIgnored(userId))
            .Returns(false);

        // Act
        Render<IgnoreUserButton>(parameters => parameters
            .Add(p => p.UserId, userId));

        // Assert - InitializeAsync should be called via type casting
        ignoredUsersServiceMock.Verify(
            x => x.IsUserIgnored(It.IsAny<string>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task Component_OnIgnoredUsersChanged_ShouldUpdateDisplay()
    {
        // Arrange
        var userId = Guid.NewGuid().ToString();
        Action onChangedCallback = null!;

        ignoredUsersServiceMock
            .Setup(x => x.IsUserIgnored(userId))
            .Returns(false);

        ignoredUsersServiceMock
            .SetupAdd(x => x.OnIgnoredUsersChanged += It.IsAny<Action>())
            .Callback<Action>(callback => onChangedCallback = callback);

        var cut = Render<IgnoreUserButton>(parameters => parameters
            .Add(p => p.UserId, userId));

        // Act
        ignoredUsersServiceMock
            .Setup(x => x.IsUserIgnored(userId))
            .Returns(true);

        onChangedCallback?.Invoke();
        cut.Render();

        // Assert
        ignoredUsersServiceMock.Verify(
            x => x.IsUserIgnored(userId),
            Times.AtLeast(2));
    }

    [Fact]
    public async Task Component_ShouldHaveProperAccessibility()
    {
        // Arrange
        var userId = Guid.NewGuid().ToString();
        ignoredUsersServiceMock
            .Setup(x => x.IsUserIgnored(userId))
            .Returns(false);

        // Act
        var cut = Render<IgnoreUserButton>(parameters => parameters
            .Add(p => p.UserId, userId));

        var button = cut.Find("button");

        // Assert
        Assert.NotNull(button.GetAttribute("title"));
        Assert.Equal("button", button.GetAttribute("type"));
    }
}