// tests/IrcChat.Client.Tests/Components/ChannelUsersListTests.cs
using Bunit;
using FluentAssertions;
using IrcChat.Client.Components;
using IrcChat.Shared.Models;
using Xunit;

namespace IrcChat.Client.Tests.Components;

public class ChannelUsersListTests : TestContext
{
    [Fact]
    public void ChannelUsersList_WithoutChannel_ShouldNotRender()
    {
        // Arrange & Act
        var cut = RenderComponent<ChannelUsersList>(parameters => parameters
            .Add(p => p.CurrentChannel, (string?)null)
            .Add(p => p.Username, "testuser")
            .Add(p => p.Users, new List<User>()));

        // Assert
        cut.Markup.Should().BeEmpty();
    }

    [Fact]
    public void ChannelUsersList_WithEmptyChannel_ShouldNotRender()
    {
        // Arrange & Act
        var cut = RenderComponent<ChannelUsersList>(parameters => parameters
            .Add(p => p.CurrentChannel, "")
            .Add(p => p.Username, "testuser")
            .Add(p => p.Users, new List<User>()));

        // Assert
        cut.Markup.Should().BeEmpty();
    }

    [Fact]
    public void ChannelUsersList_WithChannel_ShouldShowHeader()
    {
        // Arrange & Act
        var cut = RenderComponent<ChannelUsersList>(parameters => parameters
            .Add(p => p.CurrentChannel, "general")
            .Add(p => p.Username, "testuser")
            .Add(p => p.Users, new List<User>()));

        // Assert
        cut.Markup.Should().Contain("Utilisateurs");
        cut.Markup.Should().Contain("(0)");
    }

    [Fact]
    public void ChannelUsersList_WithUsers_ShouldDisplayCount()
    {
        // Arrange
        var users = new List<User>
        {
            new() { Username = "user1", ConnectedAt = DateTime.UtcNow },
            new() { Username = "user2", ConnectedAt = DateTime.UtcNow },
            new() { Username = "user3", ConnectedAt = DateTime.UtcNow }
        };

        // Act
        var cut = RenderComponent<ChannelUsersList>(parameters => parameters
            .Add(p => p.CurrentChannel, "general")
            .Add(p => p.Username, "testuser")
            .Add(p => p.Users, users));

        // Assert
        cut.Markup.Should().Contain("(3)");
    }

    [Fact]
    public void ChannelUsersList_WithUsers_ShouldListAllUsers()
    {
        // Arrange
        var users = new List<User>
        {
            new() { Username = "alice", ConnectedAt = DateTime.UtcNow },
            new() { Username = "bob", ConnectedAt = DateTime.UtcNow },
            new() { Username = "charlie", ConnectedAt = DateTime.UtcNow }
        };

        // Act
        var cut = RenderComponent<ChannelUsersList>(parameters => parameters
            .Add(p => p.CurrentChannel, "general")
            .Add(p => p.Username, "testuser")
            .Add(p => p.Users, users));

        // Assert
        cut.Markup.Should().Contain("alice");
        cut.Markup.Should().Contain("bob");
        cut.Markup.Should().Contain("charlie");
    }

    [Fact]
    public void ChannelUsersList_WithCurrentUser_ShouldHighlight()
    {
        // Arrange
        var users = new List<User>
        {
            new() { Username = "alice", ConnectedAt = DateTime.UtcNow },
            new() { Username = "testuser", ConnectedAt = DateTime.UtcNow },
            new() { Username = "bob", ConnectedAt = DateTime.UtcNow }
        };

        // Act
        var cut = RenderComponent<ChannelUsersList>(parameters => parameters
            .Add(p => p.CurrentChannel, "general")
            .Add(p => p.Username, "testuser")
            .Add(p => p.Users, users));

        // Assert
        var userItems = cut.FindAll(".user-list li");
        userItems.Should().HaveCount(3);

        var currentUserItem = userItems.FirstOrDefault(li => li.TextContent.Contains("testuser"));
        currentUserItem.Should().NotBeNull();
        currentUserItem!.ClassList.Should().Contain("current");
    }

    [Fact]
    public async Task ChannelUsersList_OnUserClick_ShouldTriggerEvent()
    {
        // Arrange
        var users = new List<User>
        {
            new() { Username = "alice", ConnectedAt = DateTime.UtcNow },
            new() { Username = "bob", ConnectedAt = DateTime.UtcNow }
        };

        string? clickedUsername = null;
        var cut = RenderComponent<ChannelUsersList>(parameters => parameters
            .Add(p => p.CurrentChannel, "general")
            .Add(p => p.Username, "testuser")
            .Add(p => p.Users, users)
            .Add(p => p.OnUserClicked, (string username) => clickedUsername = username));

        // Act
        var userItems = cut.FindAll(".user-list li");
        var aliceItem = userItems.First(li => li.TextContent.Contains("alice"));
        await cut.InvokeAsync(() => aliceItem.Click());

        // Assert
        clickedUsername.Should().Be("alice");
    }

    [Fact]
    public async Task ChannelUsersList_OnCurrentUserClick_ShouldStillTriggerEvent()
    {
        // Arrange
        var users = new List<User>
        {
            new() { Username = "testuser", ConnectedAt = DateTime.UtcNow }
        };

        string? clickedUsername = null;
        var cut = RenderComponent<ChannelUsersList>(parameters => parameters
            .Add(p => p.CurrentChannel, "general")
            .Add(p => p.Username, "testuser")
            .Add(p => p.Users, users)
            .Add(p => p.OnUserClicked, (string username) => clickedUsername = username));

        // Act
        var userItem = cut.Find(".user-list li");
        await cut.InvokeAsync(() => userItem.Click());

        // Assert
        clickedUsername.Should().Be("testuser");
    }

    [Fact]
    public void ChannelUsersList_WithNullUsers_ShouldShowZeroCount()
    {
        // Arrange & Act
        var cut = RenderComponent<ChannelUsersList>(parameters => parameters
            .Add(p => p.CurrentChannel, "general")
            .Add(p => p.Username, "testuser")
            .Add(p => p.Users, (List<User>?)null));

        // Assert
        cut.Markup.Should().Contain("(0)");
    }

    [Fact]
    public void ChannelUsersList_AllUsers_ShouldHaveOnlineStatus()
    {
        // Arrange
        var users = new List<User>
        {
            new() { Username = "user1", ConnectedAt = DateTime.UtcNow },
            new() { Username = "user2", ConnectedAt = DateTime.UtcNow }
        };

        // Act
        var cut = RenderComponent<ChannelUsersList>(parameters => parameters
            .Add(p => p.CurrentChannel, "general")
            .Add(p => p.Username, "testuser")
            .Add(p => p.Users, users));

        // Assert
        var statusDots = cut.FindAll(".status-dot.online");
        statusDots.Should().HaveCount(2);
    }

    [Fact]
    public void ChannelUsersList_UsersOrder_ShouldBePreserved()
    {
        // Arrange
        var users = new List<User>
        {
            new() { Username = "zebra", ConnectedAt = DateTime.UtcNow },
            new() { Username = "alpha", ConnectedAt = DateTime.UtcNow },
            new() { Username = "beta", ConnectedAt = DateTime.UtcNow }
        };

        // Act
        var cut = RenderComponent<ChannelUsersList>(parameters => parameters
            .Add(p => p.CurrentChannel, "general")
            .Add(p => p.Username, "testuser")
            .Add(p => p.Users, users));

        // Assert
        var userItems = cut.FindAll(".user-list li");
        userItems[0].TextContent.Should().Contain("zebra");
        userItems[1].TextContent.Should().Contain("alpha");
        userItems[2].TextContent.Should().Contain("beta");
    }

    [Fact]
    public async Task ChannelUsersList_MultipleClicks_ShouldTriggerMultipleTimes()
    {
        // Arrange
        var users = new List<User>
        {
            new() { Username = "alice", ConnectedAt = DateTime.UtcNow }
        };

        var clickCount = 0;
        var cut = RenderComponent<ChannelUsersList>(parameters => parameters
            .Add(p => p.CurrentChannel, "general")
            .Add(p => p.Username, "testuser")
            .Add(p => p.Users, users)
            .Add(p => p.OnUserClicked, (string _) => clickCount++));

        // Act
        var userItem = cut.Find(".user-list li");
        await cut.InvokeAsync(() => userItem.Click());
        await cut.InvokeAsync(() => userItem.Click());
        await cut.InvokeAsync(() => userItem.Click());

        // Assert
        clickCount.Should().Be(3);
    }

    [Fact]
    public void ChannelUsersList_WithLongUsername_ShouldRenderCorrectly()
    {
        // Arrange
        var users = new List<User>
        {
            new() { Username = "verylongusernamethatexceedstwentycharacters", ConnectedAt = DateTime.UtcNow }
        };

        // Act
        var cut = RenderComponent<ChannelUsersList>(parameters => parameters
            .Add(p => p.CurrentChannel, "general")
            .Add(p => p.Username, "testuser")
            .Add(p => p.Users, users));

        // Assert
        cut.Markup.Should().Contain("verylongusernamethatexceedstwentycharacters");
    }

    [Fact]
    public void ChannelUsersList_WithSpecialCharacters_ShouldRenderCorrectly()
    {
        // Arrange
        var users = new List<User>
        {
            new() { Username = "user_123", ConnectedAt = DateTime.UtcNow },
            new() { Username = "test-user", ConnectedAt = DateTime.UtcNow },
            new() { Username = "user.name", ConnectedAt = DateTime.UtcNow }
        };

        // Act
        var cut = RenderComponent<ChannelUsersList>(parameters => parameters
            .Add(p => p.CurrentChannel, "general")
            .Add(p => p.Username, "testuser")
            .Add(p => p.Users, users));

        // Assert
        cut.Markup.Should().Contain("user_123");
        cut.Markup.Should().Contain("test-user");
        cut.Markup.Should().Contain("user.name");
    }
}
