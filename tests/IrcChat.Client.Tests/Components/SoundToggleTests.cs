// tests/IrcChat.Client.Tests/Components/SoundToggleTests.cs
using IrcChat.Client.Components;
using IrcChat.Client.Services;
using Microsoft.Extensions.DependencyInjection;

namespace IrcChat.Client.Tests.Components;

public class SoundToggleTests : BunitContext
{
    private readonly Mock<INotificationSoundService> _notificationSoundServiceMock;

    public SoundToggleTests()
    {
        _notificationSoundServiceMock = new Mock<INotificationSoundService>();
        Services.AddSingleton(_notificationSoundServiceMock.Object);
    }

    [Fact]
    public async Task SoundToggle_WhenEnabled_ShouldDisplayBellIcon()
    {
        // Arrange
        _notificationSoundServiceMock
            .Setup(x => x.IsSoundEnabledAsync())
            .ReturnsAsync(true);

        // Act
        var cut = Render<SoundToggle>();
        await Task.Delay(100);

        // Assert
        var icon = cut.Find(".sound-icon");
        Assert.Contains("🔔", icon.TextContent);
        Assert.Contains("Désactiver les sons", cut.Find("button").GetAttribute("title"));
    }

    [Fact]
    public async Task SoundToggle_WhenDisabled_ShouldDisplayMutedBellIcon()
    {
        // Arrange
        _notificationSoundServiceMock
            .Setup(x => x.IsSoundEnabledAsync())
            .ReturnsAsync(false);

        // Act
        var cut = Render<SoundToggle>();
        await Task.Delay(100);

        // Assert
        var icon = cut.Find(".sound-icon");
        Assert.Contains("🔕", icon.TextContent);
        Assert.Contains("Activer les sons", cut.Find("button").GetAttribute("title"));
    }

    [Fact]
    public async Task SoundToggle_OnClick_ShouldToggleState()
    {
        // Arrange
        _notificationSoundServiceMock
            .Setup(x => x.IsSoundEnabledAsync())
            .ReturnsAsync(true);

        _notificationSoundServiceMock
            .Setup(x => x.ToggleSoundAsync())
            .Returns(Task.CompletedTask);

        var cut = Render<SoundToggle>();
        await Task.Delay(100);

        // Vérifier état initial
        var iconBefore = cut.Find(".sound-icon");
        Assert.Contains("🔔", iconBefore.TextContent);

        // Act
        var button = cut.Find("button");
        await cut.InvokeAsync(() => button.Click());

        // Assert
        var iconAfter = cut.Find(".sound-icon");
        Assert.Contains("🔕", iconAfter.TextContent);
        Assert.Contains("Activer les sons", cut.Find("button").GetAttribute("title"));
    }

    [Fact]
    public async Task SoundToggle_OnClick_ShouldCallService()
    {
        // Arrange
        _notificationSoundServiceMock
            .Setup(x => x.IsSoundEnabledAsync())
            .ReturnsAsync(true);

        _notificationSoundServiceMock
            .Setup(x => x.ToggleSoundAsync())
            .Returns(Task.CompletedTask);

        var cut = Render<SoundToggle>();
        await Task.Delay(100);

        // Act
        var button = cut.Find("button");
        await cut.InvokeAsync(() => button.Click());

        // Assert
        _notificationSoundServiceMock.Verify(x => x.ToggleSoundAsync(), Times.Once);
    }

    [Fact]
    public async Task SoundToggle_OnMultipleClicks_ShouldToggleMultipleTimes()
    {
        // Arrange
        _notificationSoundServiceMock
            .Setup(x => x.IsSoundEnabledAsync())
            .ReturnsAsync(true);

        _notificationSoundServiceMock
            .Setup(x => x.ToggleSoundAsync())
            .Returns(Task.CompletedTask);

        var cut = Render<SoundToggle>();
        await Task.Delay(100);

        var button = cut.Find("button");

        // Act - Premier clic (désactiver)
        await cut.InvokeAsync(() => button.Click());
        var iconAfterFirst = cut.Find(".sound-icon");
        Assert.Contains("🔕", iconAfterFirst.TextContent);

        // Act - Deuxième clic (réactiver)
        await cut.InvokeAsync(() => button.Click());
        var iconAfterSecond = cut.Find(".sound-icon");
        Assert.Contains("🔔", iconAfterSecond.TextContent);

        // Act - Troisième clic (désactiver à nouveau)
        await cut.InvokeAsync(() => button.Click());
        var iconAfterThird = cut.Find(".sound-icon");
        Assert.Contains("🔕", iconAfterThird.TextContent);

        // Assert
        _notificationSoundServiceMock.Verify(x => x.ToggleSoundAsync(), Times.Exactly(3));
    }

    [Fact]
    public async Task SoundToggle_HasCorrectCssClass()
    {
        // Arrange
        _notificationSoundServiceMock
            .Setup(x => x.IsSoundEnabledAsync())
            .ReturnsAsync(true);

        // Act
        var cut = Render<SoundToggle>();
        await Task.Delay(100);

        // Assert
        var button = cut.Find("button");
        Assert.Contains("sound-toggle", button.ClassName);
    }

    [Fact]
    public async Task SoundToggle_IconHasCorrectCssClass()
    {
        // Arrange
        _notificationSoundServiceMock
            .Setup(x => x.IsSoundEnabledAsync())
            .ReturnsAsync(true);

        // Act
        var cut = Render<SoundToggle>();
        await Task.Delay(100);

        // Assert
        var icon = cut.Find(".sound-icon");
        Assert.NotNull(icon);
    }

    [Fact]
    public async Task SoundToggle_OnClick_ShouldStopPropagation()
    {
        // Arrange
        _notificationSoundServiceMock
            .Setup(x => x.IsSoundEnabledAsync())
            .ReturnsAsync(true);

        _notificationSoundServiceMock
            .Setup(x => x.ToggleSoundAsync())
            .Returns(Task.CompletedTask);

        var cut = Render<SoundToggle>();
        await Task.Delay(100);

        // Act & Assert
        var button = cut.Find("button");

        // Vérifier que stopPropagation est défini
        Assert.Contains("blazor:onclick:stopPropagation", cut.Markup);
    }

    [Fact]
    public async Task SoundToggle_WhenActivated_ShouldPlayTestSound()
    {
        // Arrange
        _notificationSoundServiceMock
            .Setup(x => x.IsSoundEnabledAsync())
            .ReturnsAsync(false); // Désactivé au départ

        _notificationSoundServiceMock
            .Setup(x => x.ToggleSoundAsync())
            .Returns(Task.CompletedTask);

        _notificationSoundServiceMock
            .Setup(x => x.PlaySoundAsync())
            .Returns(Task.CompletedTask);

        var cut = Render<SoundToggle>();
        await Task.Delay(100);

        // Act - Activer les sons
        var button = cut.Find("button");
        await cut.InvokeAsync(() => button.Click());

        // Assert - PlaySoundAsync doit être appelé (son de test)
        _notificationSoundServiceMock.Verify(x => x.PlaySoundAsync(), Times.Once);
    }

    [Fact]
    public async Task SoundToggle_WhenDeactivated_ShouldNotPlayTestSound()
    {
        // Arrange
        _notificationSoundServiceMock
            .Setup(x => x.IsSoundEnabledAsync())
            .ReturnsAsync(true); // Activé au départ

        _notificationSoundServiceMock
            .Setup(x => x.ToggleSoundAsync())
            .Returns(Task.CompletedTask);

        _notificationSoundServiceMock
            .Setup(x => x.PlaySoundAsync())
            .Returns(Task.CompletedTask);

        var cut = Render<SoundToggle>();
        await Task.Delay(100);

        // Act - Désactiver les sons
        var button = cut.Find("button");
        await cut.InvokeAsync(() => button.Click());

        // Assert - PlaySoundAsync ne doit PAS être appelé
        _notificationSoundServiceMock.Verify(x => x.PlaySoundAsync(), Times.Never);
    }
}