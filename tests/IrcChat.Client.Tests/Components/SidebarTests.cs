// tests/IrcChat.Client.Tests/Components/SidebarTests.cs
using IrcChat.Client.Components;
using IrcChat.Client.Services;
using IrcChat.Shared.Models;
using Microsoft.Extensions.DependencyInjection;

namespace IrcChat.Client.Tests.Components;

public class SidebarTests : BunitContext
{
    private readonly Mock<IChannelUnreadCountService> _channelUnreadCountServiceMock;
    public SidebarTests()
    {
        _channelUnreadCountServiceMock = new Mock<IChannelUnreadCountService>();

        Services.AddSingleton(_channelUnreadCountServiceMock.Object);
    }

    [Fact]
    public void Sidebar_WhenOpen_ShouldHaveOpenClass()
    {
        // Arrange & Act
        var cut = Render<Sidebar>(parameters => parameters
            .Add(p => p.IsOpen, true)
            .Add(p => p.Username, "TestUser")
            .Add(p => p.Channels, []));

        // Assert
        Assert.Contains("open", cut.Find(".sidebar").ClassList);
    }

    [Fact]
    public void Sidebar_WhenClosed_ShouldHaveClosedClass()
    {
        // Arrange & Act
        var cut = Render<Sidebar>(parameters => parameters
            .Add(p => p.IsOpen, false)
            .Add(p => p.Username, "TestUser")
            .Add(p => p.Channels, []));

        // Assert
        Assert.Contains("closed", cut.Find(".sidebar").ClassList);
    }

    [Fact]
    public void Sidebar_ShouldDisplayUsername()
    {
        // Arrange & Act
        var cut = Render<Sidebar>(parameters => parameters
            .Add(p => p.IsOpen, true)
            .Add(p => p.Username, "JohnDoe")
            .Add(p => p.Channels, []));

        // Assert
        Assert.Contains("JohnDoe", cut.Markup);
    }

    [Fact]
    public void Sidebar_WithAvatar_ShouldDisplayAvatar()
    {
        // Arrange & Act
        var cut = Render<Sidebar>(parameters => parameters
            .Add(p => p.IsOpen, true)
            .Add(p => p.Username, "TestUser")
            .Add(p => p.AvatarUrl, "https://example.com/avatar.jpg")
            .Add(p => p.Channels, []));

        // Assert
        var avatar = cut.Find(".avatar");
        Assert.Equal("https://example.com/avatar.jpg", avatar.GetAttribute("src"));
    }

    [Fact]
    public void Sidebar_WithoutAvatar_ShouldDisplayPlaceholder()
    {
        // Arrange & Act
        var cut = Render<Sidebar>(parameters => parameters
            .Add(p => p.IsOpen, true)
            .Add(p => p.Username, "TestUser")
            .Add(p => p.AvatarUrl, null)
            .Add(p => p.Channels, []));

        // Assert
        Assert.NotNull(cut.Find(".avatar-placeholder"));
    }

    [Fact]
    public void Sidebar_WhenOAuthUser_ShouldDisplayVerifiedBadge()
    {
        // Arrange & Act
        var cut = Render<Sidebar>(parameters => parameters
            .Add(p => p.IsOpen, true)
            .Add(p => p.Username, "TestUser")
            .Add(p => p.IsOAuthUser, true)
            .Add(p => p.Channels, []));

        // Assert
        Assert.Contains("✓ Réservé", cut.Markup);
    }

    [Fact]
    public void Sidebar_WhenAdmin_ShouldDisplayAdminBadge()
    {
        // Arrange & Act
        var cut = Render<Sidebar>(parameters => parameters
            .Add(p => p.IsOpen, true)
            .Add(p => p.Username, "AdminUser")
            .Add(p => p.IsOAuthUser, true)
            .Add(p => p.IsAdmin, true)
            .Add(p => p.Channels, []));

        // Assert
        Assert.Contains("⚡ Admin", cut.Markup);
    }

    [Fact]
    public void Sidebar_WhenGuestUser_ShouldDisplayGuestBadge()
    {
        // Arrange & Act
        var cut = Render<Sidebar>(parameters => parameters
            .Add(p => p.IsOpen, true)
            .Add(p => p.Username, "GuestUser")
            .Add(p => p.IsOAuthUser, false)
            .Add(p => p.Channels, []));

        // Assert
        Assert.Contains("Invité", cut.Markup);
    }

    [Fact]
    public void Sidebar_ShouldDisplayChannelsList()
    {
        // Arrange
        var channels = new List<Channel>
        {
            new() { Id = Guid.NewGuid(), Name = "general", CreatedBy = "system", CreatedAt = DateTime.UtcNow },
            new() { Id = Guid.NewGuid(), Name = "random", CreatedBy = "system", CreatedAt = DateTime.UtcNow },
            new() { Id = Guid.NewGuid(), Name = "tech", CreatedBy = "user1", CreatedAt = DateTime.UtcNow },
        };

        // Act
        var cut = Render<Sidebar>(parameters => parameters
            .Add(p => p.IsOpen, true)
            .Add(p => p.Username, "TestUser")
            .Add(p => p.Channels, channels));

        // Assert
        Assert.Contains("general", cut.Markup);
        Assert.Contains("random", cut.Markup);
        Assert.Contains("tech", cut.Markup);
    }

    [Fact]
    public void Sidebar_CurrentChannel_ShouldBeHighlighted()
    {
        // Arrange
        var channels = new List<Channel>
        {
            new() { Id = Guid.NewGuid(), Name = "general", CreatedBy = "system", CreatedAt = DateTime.UtcNow },
            new() { Id = Guid.NewGuid(), Name = "random", CreatedBy = "system", CreatedAt = DateTime.UtcNow },
        };

        // Act
        var cut = Render<Sidebar>(parameters => parameters
            .Add(p => p.IsOpen, true)
            .Add(p => p.Username, "TestUser")
            .Add(p => p.Channels, channels)
            .Add(p => p.CurrentChannel, "general"));

        // Assert
        var activeChannel = cut.FindAll(".channel-list li.active");
        Assert.Single(activeChannel);
        Assert.Contains("general", activeChannel[0].TextContent);
    }

    [Fact]
    public void Sidebar_ShouldDisplayPrivateConversations()
    {
        // Arrange
        var conversations = new List<PrivateConversation>
        {
            new()
            {
                OtherUser = new User
                {
                    UserId = "Friend1",
                    Username = "Friend1",
                },
                LastMessage = "Hello",
                LastMessageTime = DateTime.UtcNow,
                UnreadCount = 2,
            },
            new()
            {
                OtherUser = new User
                {
                    UserId = "Friend2",
                    Username = "Friend2"
                },
                LastMessage = "Hi",
                LastMessageTime = DateTime.UtcNow,
                UnreadCount = 0
            },
        };

        // Act
        var cut = Render<Sidebar>(parameters => parameters
            .Add(p => p.IsOpen, true)
            .Add(p => p.Username, "TestUser")
            .Add(p => p.Channels, [])
            .Add(p => p.PrivateConversations, conversations));

        // Assert
        Assert.Contains("Friend1", cut.Markup);
        Assert.Contains("Friend2", cut.Markup);
    }

    [Fact]
    public async Task Sidebar_UserInfoClick_ShouldInvokeCallback()
    {
        // Arrange
        var userInfoClickedInvoked = false;

        var cut = Render<Sidebar>(parameters => parameters
            .Add(p => p.IsOpen, true)
            .Add(p => p.Username, "TestUser")
            .Add(p => p.Channels, [])
            .Add(p => p.OnUserInfoClicked, () => userInfoClickedInvoked = true));

        // Act
        var userInfo = cut.Find(".user-info");
        await cut.InvokeAsync(() => userInfo.Click());

        // Assert
        Assert.True(userInfoClickedInvoked);
    }

    [Fact]
    public void Sidebar_WithUnreadMessages_ShouldDisplayBadge()
    {
        // Arrange
        var conversations = new List<PrivateConversation>
        {
            new()
            {
                OtherUser = new User
                {
                    UserId = "Friend",
                    Username = "Friend"
                },
                LastMessage = "Hello",
                LastMessageTime = DateTime.UtcNow,
                UnreadCount = 5
            },
        };

        // Act
        var cut = Render<Sidebar>(parameters => parameters
            .Add(p => p.IsOpen, true)
            .Add(p => p.Username, "TestUser")
            .Add(p => p.Channels, [])
            .Add(p => p.PrivateConversations, conversations));

        // Assert
        Assert.Contains("5", cut.Markup);
        Assert.NotNull(cut.Find(".unread-count"));
    }

    [Fact]
    public async Task Sidebar_WithChannelsConnectedUsersCount_ShouldDisplayCount()
    {
        // Arrange
        var channels = new List<Channel>
    {
        new() { Id = Guid.NewGuid(), Name = "general", CreatedBy = "system", CreatedAt = DateTime.UtcNow, ConnectedUsersCount = 10 },
        new() { Id = Guid.NewGuid(), Name = "random", CreatedBy = "system", CreatedAt = DateTime.UtcNow, ConnectedUsersCount = 5 },
    };

        // Act
        var cut = Render<Sidebar>(parameters => parameters
            .Add(p => p.IsOpen, true)
            .Add(p => p.Username, "TestUser")
            .Add(p => p.Channels, channels));

        // Assert
        Assert.Contains("10", cut.Markup);
        Assert.Contains("5", cut.Markup);
    }

    [Fact]
    public async Task Sidebar_BrowseChannelsButton_ShouldBeVisible()
    {
        // Arrange & Act
        var cut = Render<Sidebar>(parameters => parameters
            .Add(p => p.IsOpen, true)
            .Add(p => p.Username, "TestUser")
            .Add(p => p.Channels, []));

        // Assert
        Assert.NotNull(cut.Find(".btn-browse-channels"));
        Assert.Contains("Parcourir", cut.Markup);
    }

    [Fact]
    public async Task Sidebar_OnBrowseChannelsClick_ShouldInvokeCallback()
    {
        // Arrange
        var browseClicked = false;

        var cut = Render<Sidebar>(parameters => parameters
            .Add(p => p.IsOpen, true)
            .Add(p => p.Username, "TestUser")
            .Add(p => p.Channels, [])
            .Add(p => p.OnBrowseChannelsClicked, () =>
            {
                browseClicked = true;
                return Task.CompletedTask;
            }));

        // Act
        var browseButton = cut.Find(".btn-browse-channels");
        await cut.InvokeAsync(() => browseButton.Click());

        // Assert
        Assert.True(browseClicked);
    }

    [Fact]
    public async Task Sidebar_WhenNoChannels_ShouldShowEmptyState()
    {
        // Arrange & Act
        var cut = Render<Sidebar>(parameters => parameters
            .Add(p => p.IsOpen, true)
            .Add(p => p.Username, "TestUser")
            .Add(p => p.Channels, []));

        // Assert
        Assert.Contains("Aucun salon rejoint", cut.Markup);
    }

    [Fact]
    public void Sidebar_WithChannels_ShouldDisplayLeaveButtons()
    {
        // Arrange
        var channels = new List<Channel>
    {
        new() { Id = Guid.NewGuid(), Name = "general", CreatedBy = "system", CreatedAt = DateTime.UtcNow, ConnectedUsersCount = 5 },
        new() { Id = Guid.NewGuid(), Name = "random", CreatedBy = "system", CreatedAt = DateTime.UtcNow, ConnectedUsersCount = 3 },
    };

        // Act
        var cut = Render<Sidebar>(parameters => parameters
            .Add(p => p.IsOpen, true)
            .Add(p => p.Username, "TestUser")
            .Add(p => p.Channels, channels));

        // Assert
        var leaveButtons = cut.FindAll(".btn-leave-channel");
        Assert.Equal(2, leaveButtons.Count);
    }

    [Fact]
    public async Task Sidebar_OnLeaveButtonClick_ShouldInvokeCallback()
    {
        // Arrange
        var channels = new List<Channel>
    {
        new() { Id = Guid.NewGuid(), Name = "general", CreatedBy = "system", CreatedAt = DateTime.UtcNow, ConnectedUsersCount = 5 },
    };

        var leftChannel = string.Empty;

        var cut = Render<Sidebar>(parameters => parameters
            .Add(p => p.IsOpen, true)
            .Add(p => p.Username, "TestUser")
            .Add(p => p.Channels, channels)
            .Add(p => p.OnChannelLeave, (channelName) =>
            {
                leftChannel = channelName;
                return Task.CompletedTask;
            }));

        // Act
        var leaveButton = cut.Find(".btn-leave-channel");
        await cut.InvokeAsync(() => leaveButton.Click());

        // Assert
        Assert.Equal("general", leftChannel);
    }

    [Fact]
    public async Task Sidebar_LeaveButton_ShouldNotTriggerChannelSelection()
    {
        // Arrange
        var channels = new List<Channel>
    {
        new() { Id = Guid.NewGuid(), Name = "general", CreatedBy = "system", CreatedAt = DateTime.UtcNow, ConnectedUsersCount = 5 },
    };

        var channelSelected = false;
        var channelLeft = false;

        var cut = Render<Sidebar>(parameters => parameters
            .Add(p => p.IsOpen, true)
            .Add(p => p.Username, "TestUser")
            .Add(p => p.Channels, channels)
            .Add(p => p.OnChannelSelected, (channelName) =>
            {
                channelSelected = true;
                return Task.CompletedTask;
            })
            .Add(p => p.OnChannelLeave, (channelName) =>
            {
                channelLeft = true;
                return Task.CompletedTask;
            }));

        // Act - Cliquer sur le bouton de fermeture
        var leaveButton = cut.Find(".btn-leave-channel");
        await cut.InvokeAsync(() => leaveButton.Click());

        // Assert - Seulement le callback Leave doit être invoqué
        Assert.False(channelSelected);
        Assert.True(channelLeft);
    }
}