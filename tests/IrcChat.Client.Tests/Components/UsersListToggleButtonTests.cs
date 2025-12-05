// tests/IrcChat.Client.Tests/Components/UsersListToggleButtonTests.cs
using IrcChat.Client.Components;

namespace IrcChat.Client.Tests.Components;

public class UsersListToggleButtonTests : BunitContext
{
    [Fact]
    public void UsersListToggleButton_WhenRendered_ShouldShowUserCount()
    {
        // Arrange & Act
        var cut = Render<UsersListToggleButton>(parameters => parameters
            .Add(p => p.IsOpen, false)
            .Add(p => p.UserCount, 5));

        // Assert
        Assert.Contains("5", cut.Markup);
        Assert.Contains("user-count", cut.Markup);
    }

    [Fact]
    public void UsersListToggleButton_WhenClosed_ShouldNotHaveOpenClass()
    {
        // Arrange & Act
        var cut = Render<UsersListToggleButton>(parameters => parameters
            .Add(p => p.IsOpen, false)
            .Add(p => p.UserCount, 3));

        // Assert
        var button = cut.Find(".users-toggle-btn");
        Assert.DoesNotContain("open", button.ClassName);
    }

    [Fact]
    public void UsersListToggleButton_WhenOpen_ShouldHaveOpenClass()
    {
        // Arrange & Act
        var cut = Render<UsersListToggleButton>(parameters => parameters
            .Add(p => p.IsOpen, true)
            .Add(p => p.UserCount, 3));

        // Assert
        var button = cut.Find(".users-toggle-btn");
        Assert.Contains("open", button.ClassName);
    }

    [Fact]
    public async Task UsersListToggleButton_WhenClicked_ShouldInvokeCallback()
    {
        // Arrange
        var toggledState = false;
        var cut = Render<UsersListToggleButton>(parameters => parameters
            .Add(p => p.IsOpen, false)
            .Add(p => p.UserCount, 5)
            .Add(p => p.OnToggle, newState => toggledState = newState));

        // Act
        var button = cut.Find(".users-toggle-btn");
        await cut.InvokeAsync(() => button.Click());

        // Assert
        Assert.True(toggledState);
    }

    [Fact]
    public async Task UsersListToggleButton_WhenClickedMultipleTimes_ShouldToggleState()
    {
        // Arrange
        var isOpen = false;
        var cut = Render<UsersListToggleButton>(parameters => parameters
            .Add(p => p.IsOpen, isOpen)
            .Add(p => p.UserCount, 5)
            .Add(p => p.OnToggle, newState => isOpen = newState));

        var button = cut.Find(".users-toggle-btn");

        // Act - Premier clic (ouvrir)
        await cut.InvokeAsync(() => button.Click());
        Assert.True(isOpen);

        // Act - Deuxième clic (fermer)
        cut.Render(parameters => parameters
            .Add(p => p.IsOpen, isOpen));
        await cut.InvokeAsync(() => button.Click());

        // Assert
        Assert.False(isOpen);
    }

    [Fact]
    public void UsersListToggleButton_WithZeroUsers_ShouldShowZero()
    {
        // Arrange & Act
        var cut = Render<UsersListToggleButton>(parameters => parameters
            .Add(p => p.IsOpen, false)
            .Add(p => p.UserCount, 0));

        // Assert
        Assert.Contains("0", cut.Markup);
    }

    [Fact]
    public void UsersListToggleButton_WithLargeUserCount_ShouldDisplayCorrectly()
    {
        // Arrange & Act
        var cut = Render<UsersListToggleButton>(parameters => parameters
            .Add(p => p.IsOpen, false)
            .Add(p => p.UserCount, 999));

        // Assert
        Assert.Contains("999", cut.Markup);
    }

    [Fact]
    public void UsersListToggleButton_WhenClosed_ShouldShowClosedTitle()
    {
        // Arrange & Act
        var cut = Render<UsersListToggleButton>(parameters => parameters
            .Add(p => p.IsOpen, false)
            .Add(p => p.UserCount, 5));

        // Assert
        var button = cut.Find(".users-toggle-btn");
        Assert.Equal("Afficher les utilisateurs", button.GetAttribute("title"));
    }

    [Fact]
    public void UsersListToggleButton_WhenOpen_ShouldShowOpenTitle()
    {
        // Arrange & Act
        var cut = Render<UsersListToggleButton>(parameters => parameters
            .Add(p => p.IsOpen, true)
            .Add(p => p.UserCount, 5));

        // Assert
        var button = cut.Find(".users-toggle-btn");
        Assert.Equal("Masquer les utilisateurs", button.GetAttribute("title"));
    }

    [Fact]
    public void UsersListToggleButton_ShouldContainUsersIcon()
    {
        // Arrange & Act
        var cut = Render<UsersListToggleButton>(parameters => parameters
            .Add(p => p.IsOpen, false)
            .Add(p => p.UserCount, 5));

        // Assert
        Assert.Contains("<svg", cut.Markup);
        Assert.Contains("viewBox=\"0 0 24 24\"", cut.Markup);
    }

    [Fact]
    public async Task UsersListToggleButton_OnToggle_ShouldPassOppositeState()
    {
        // Arrange
        var receivedState = false;
        var cut = Render<UsersListToggleButton>(parameters => parameters
            .Add(p => p.IsOpen, false)
            .Add(p => p.UserCount, 5)
            .Add(p => p.OnToggle, state => receivedState = state));

        // Act
        await cut.InvokeAsync(() => cut.Find(".users-toggle-btn").Click());

        // Assert
        Assert.True(receivedState); // Devrait être true car IsOpen était false
    }

    [Fact]
    public void UsersListToggleButton_WithDifferentStates_ShouldRenderDifferently()
    {
        // Arrange & Act - État fermé
        var cutClosed = Render<UsersListToggleButton>(parameters => parameters
            .Add(p => p.IsOpen, false)
            .Add(p => p.UserCount, 5));

        var cutOpen = Render<UsersListToggleButton>(parameters => parameters
            .Add(p => p.IsOpen, true)
            .Add(p => p.UserCount, 5));

        // Assert
        var buttonClosed = cutClosed.Find(".users-toggle-btn");
        var buttonOpen = cutOpen.Find(".users-toggle-btn");

        Assert.DoesNotContain("open", buttonClosed.ClassName);
        Assert.Contains("open", buttonOpen.ClassName);
    }
}