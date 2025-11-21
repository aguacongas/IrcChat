using Bunit;
using IrcChat.Client.Components;
using IrcChat.Client.Services;
using IrcChat.Shared.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace IrcChat.Client.Tests.Components;

public class ChannelUsersListTests : TestContext
{
    private readonly Mock<IIgnoredUsersService> _ignoredUsersServiceMock;
    private readonly List<User> _testUsers;

    public ChannelUsersListTests()
    {
        _ignoredUsersServiceMock = new Mock<IIgnoredUsersService>();

        Services.AddSingleton(_ignoredUsersServiceMock.Object);
        Services.AddSingleton(new Mock<ILogger<ChannelUsersList>>().Object);

        _testUsers =
        [
            new() { UserId = Guid.NewGuid().ToString(), Username = "user1" },
            new() { UserId = Guid.NewGuid().ToString(), Username = "user2" },
            new() { UserId = Guid.NewGuid().ToString(), Username = "user3" }
        ];
    }

    [Fact]
    public async Task Component_WhenRendered_ShouldDisplayAllUsers()
    {
        // Arrange
        _ignoredUsersServiceMock
            .Setup(x => x.IsUserIgnored(It.IsAny<string>()))
            .Returns(false);

        // Act
        var cut = RenderComponent<ChannelUsersList>(parameters => parameters
            .Add(p => p.Users, _testUsers));

        // Assert
        Assert.Contains("user1", cut.Markup);
        Assert.Contains("user2", cut.Markup);
        Assert.Contains("user3", cut.Markup);
    }

    [Fact]
    public async Task Component_WithEmptyList_ShouldShowEmptyState()
    {
        // Arrange & Act
        var cut = RenderComponent<ChannelUsersList>(parameters => parameters
            .Add(p => p.Users, []));

        // Assert
        Assert.Contains("Aucun utilisateur connecté", cut.Markup);
    }

    [Fact]
    public async Task Component_WhenUserIgnored_ShouldShowIndicator()
    {
        // Arrange
        var ignoredUserId = _testUsers[0].UserId;

        _ignoredUsersServiceMock
            .Setup(x => x.IsUserIgnored(ignoredUserId))
            .Returns(true);

        _ignoredUsersServiceMock
            .Setup(x => x.IsUserIgnored(It.Is<string>(id => id != ignoredUserId)))
            .Returns(false);

        // Act
        var cut = RenderComponent<ChannelUsersList>(parameters => parameters
            .Add(p => p.Users, _testUsers));

        // Assert
        var ignoredItems = cut.FindAll(".user-item.ignored");
        Assert.Single(ignoredItems);
        Assert.Contains("🚫", cut.Markup);
    }

    [Fact]
    public async Task Component_IgnoredUserShouldHaveStrikethrough()
    {
        // Arrange
        var ignoredUserId = _testUsers[1].UserId;

        _ignoredUsersServiceMock
            .Setup(x => x.IsUserIgnored(ignoredUserId))
            .Returns(true);

        _ignoredUsersServiceMock
            .Setup(x => x.IsUserIgnored(It.Is<string>(id => id != ignoredUserId)))
            .Returns(false);

        // Act
        var cut = RenderComponent<ChannelUsersList>(parameters => parameters
            .Add(p => p.Users, _testUsers));

        // Assert
        var ignoredItems = cut.FindAll(".user-item.ignored");
        Assert.NotEmpty(ignoredItems);
    }

    [Fact]
    public async Task Component_OnIgnoredUsersChanged_ShouldRefresh()
    {
        // Arrange
        var ignoredUserId = _testUsers[0].UserId;
        Action onChangedCallback = null!;

        _ignoredUsersServiceMock
            .Setup(x => x.IsUserIgnored(It.IsAny<string>()))
            .Returns(false);

        _ignoredUsersServiceMock
            .SetupAdd(x => x.OnIgnoredUsersChanged += It.IsAny<Action>())
            .Callback<Action>(callback => onChangedCallback = callback);

        var cut = RenderComponent<ChannelUsersList>(parameters => parameters
            .Add(p => p.Users, _testUsers));

        // Act
        _ignoredUsersServiceMock
            .Setup(x => x.IsUserIgnored(ignoredUserId))
            .Returns(true);

        _ignoredUsersServiceMock
            .Setup(x => x.IsUserIgnored(It.Is<string>(id => id != ignoredUserId)))
            .Returns(false);

        onChangedCallback?.Invoke();
        cut.Render();

        // Assert
        Assert.Contains("🚫", cut.Markup);
    }

    [Fact]
    public async Task Component_WhenUserListUpdated_ShouldRefresh()
    {
        // Arrange
        var users = new List<User>
        {
            new() { UserId = Guid.NewGuid().ToString(), Username = "user1" }
        };

        _ignoredUsersServiceMock
            .Setup(x => x.IsUserIgnored(It.IsAny<string>()))
            .Returns(false);

        var cut = RenderComponent<ChannelUsersList>(parameters => parameters
            .Add(p => p.Users, users));

        Assert.Contains("user1", cut.Markup);

        // Act
        var updatedUsers = new List<User>
        {
            new() { UserId = Guid.NewGuid().ToString(), Username = "user1" },
            new() { UserId = Guid.NewGuid().ToString(), Username = "user2" }
        };

        cut.SetParametersAndRender(parameters => parameters
            .Add(p => p.Users, updatedUsers));

        // Assert
        Assert.Contains("user1", cut.Markup);
        Assert.Contains("user2", cut.Markup);
    }

    [Fact]
    public async Task Component_ShouldDisplayCorrectUserNames()
    {
        // Arrange
        _ignoredUsersServiceMock
            .Setup(x => x.IsUserIgnored(It.IsAny<string>()))
            .Returns(false);

        // Act
        var cut = RenderComponent<ChannelUsersList>(parameters => parameters
            .Add(p => p.Users, _testUsers));

        // Assert
        var userNames = cut.FindAll(".user-name");
        Assert.Equal(3, userNames.Count);
    }
}