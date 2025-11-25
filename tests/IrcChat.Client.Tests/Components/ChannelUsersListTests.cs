using System.Net.Http.Json;
using Bunit;
using IrcChat.Client.Components;
using IrcChat.Client.Services;
using IrcChat.Shared.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using RichardSzalay.MockHttp;
using Xunit;

namespace IrcChat.Client.Tests.Components;

public class ChannelUsersListTests : BunitContext
{
    private readonly MockHttpMessageHandler _mockHttp;
    private readonly Mock<IIgnoredUsersService> _ignoredUsersServiceMock;
    private readonly Mock<ILogger<ChannelUsersList>> _loggerMock;

    public ChannelUsersListTests()
    {
        _mockHttp = new MockHttpMessageHandler();
        _ignoredUsersServiceMock = new Mock<IIgnoredUsersService>();
        _loggerMock = new Mock<ILogger<ChannelUsersList>>();

        // Configuration par défaut du service ignoré
        _ignoredUsersServiceMock.Setup(x => x.InitializeAsync()).Verifiable();
        _ignoredUsersServiceMock.Setup(x => x.IsUserIgnored(It.IsAny<string>())).Returns(false);

        var httpClient = _mockHttp.ToHttpClient();
        httpClient.BaseAddress = new Uri("https://localhost:7000");
        Services.AddScoped(sp => httpClient);
        Services.AddScoped(_ => _ignoredUsersServiceMock.Object);
        Services.AddScoped(_ => _loggerMock.Object);
    }

    [Fact]
    public void Component_WhenRendered_ShouldDisplayUsersList()
    {
        // Arrange
        var users = new List<User>
        {
            new() { UserId = "user1", Username = "Alice" },
            new() { UserId = "user2", Username = "Bob" }
        };

        _mockHttp
            .When(HttpMethod.Get, "*/api/channels/general/muted-users")
            .Respond(System.Net.HttpStatusCode.OK,
                JsonContent.Create(new List<dynamic>()));

        // Act
        var cut = Render<ChannelUsersList>(parameters => parameters
            .Add(p => p.Users, users)
            .Add(p => p.ChannelName, "general")
            .Add(p => p.Username, "Alice")
            .Add(p => p.CanModifyChannel, false));

        cut.WaitForState(() => !cut.Markup.Contains("Aucun utilisateur connecté"), TimeSpan.FromSeconds(2));
        cut.Render();

        // Assert
        Assert.Contains("Alice", cut.Markup);
        Assert.Contains("Bob", cut.Markup);
        Assert.Contains("Utilisateurs (2)", cut.Markup);
    }

    [Fact]
    public void Component_WhenNoUsers_ShouldShowEmptyState()
    {
        // Arrange
        var users = new List<User>();

        _mockHttp
            .When(HttpMethod.Get, "*/api/channels/general/muted-users")
            .Respond(System.Net.HttpStatusCode.OK,
                JsonContent.Create(new List<dynamic>()));

        // Act
        var cut = Render<ChannelUsersList>(parameters => parameters
            .Add(p => p.Users, users)
            .Add(p => p.ChannelName, "general")
            .Add(p => p.Username, "Alice")
            .Add(p => p.CanModifyChannel, false));

        cut.WaitForState(() => cut.Markup.Contains("Aucun utilisateur connecté"), TimeSpan.FromSeconds(2));
        cut.Render();

        // Assert
        Assert.Contains("Aucun utilisateur connecté", cut.Markup);
    }

    [Fact]
    public void Component_WhenUserIsIgnored_ShouldShowIgnoreIndicator()
    {
        // Arrange
        var users = new List<User>
        {
            new() { UserId = "user1", Username = "Alice" }
        };

        _ignoredUsersServiceMock.Setup(x => x.IsUserIgnored("user1")).Returns(true);

        _mockHttp
            .When(HttpMethod.Get, "*/api/channels/general/muted-users")
            .Respond(System.Net.HttpStatusCode.OK,
                JsonContent.Create(new List<dynamic>()));

        // Act
        var cut = Render<ChannelUsersList>(parameters => parameters
            .Add(p => p.Users, users)
            .Add(p => p.ChannelName, "general")
            .Add(p => p.Username, "Bob")
            .Add(p => p.CanModifyChannel, false));

        cut.WaitForState(() => cut.Markup.Contains("ignored"), TimeSpan.FromSeconds(2));
        cut.Render();

        // Assert
        Assert.Contains("🚫", cut.Markup);
        var liElement = cut.Find("li.user-item");
        Assert.NotNull(liElement);
        Assert.Contains("ignored", liElement.ClassName);
    }

    [Fact]
    public void Component_WhenCannotModifyChannel_ShouldNotShowMuteButtons()
    {
        // Arrange
        var users = new List<User>
        {
            new() { UserId = "user1", Username = "Alice" },
            new() { UserId = "user2", Username = "Bob" }
        };

        _mockHttp
            .When(HttpMethod.Get, "*/api/channels/general/muted-users")
            .Respond(System.Net.HttpStatusCode.OK,
                JsonContent.Create(new List<dynamic>()));

        // Act
        var cut = Render<ChannelUsersList>(parameters => parameters
            .Add(p => p.Users, users)
            .Add(p => p.ChannelName, "general")
            .Add(p => p.Username, "Charlie")
            .Add(p => p.CanModifyChannel, false));

        cut.WaitForState(() => cut.Markup.Contains("Alice"), TimeSpan.FromSeconds(2));
        cut.Render();

        // Assert
        var muteButtons = cut.FindAll("button.btn-mute");
        Assert.Empty(muteButtons);
    }

    [Fact]
    public void Component_WhenCanModifyChannel_ShouldShowMuteButtonsForOtherUsers()
    {
        // Arrange
        var users = new List<User>
        {
            new() { UserId = "user1", Username = "Alice" },
            new() { UserId = "user2", Username = "Bob" }
        };

        _mockHttp
            .When(HttpMethod.Get, "*/api/channels/general/muted-users")
            .Respond(System.Net.HttpStatusCode.OK,
                JsonContent.Create(new List<dynamic>()));

        // Act
        var cut = Render<ChannelUsersList>(parameters => parameters
            .Add(p => p.Users, users)
            .Add(p => p.ChannelName, "general")
            .Add(p => p.Username, "Charlie")
            .Add(p => p.CanModifyChannel, true));

        cut.WaitForState(() => cut.Markup.Contains("Alice"), TimeSpan.FromSeconds(2));
        cut.Render();

        // Assert
        var muteButtons = cut.FindAll("button.btn-mute");
        Assert.Equal(2, muteButtons.Count);
    }

    [Fact]
    public void Component_WhenCurrentUser_ShouldNotShowMuteButton()
    {
        // Arrange
        var users = new List<User>
        {
            new() { UserId = "user1", Username = "Alice" },
            new() { UserId = "user2", Username = "Bob" }
        };

        _mockHttp
            .When(HttpMethod.Get, "*/api/channels/general/muted-users")
            .Respond(System.Net.HttpStatusCode.OK,
                JsonContent.Create(new List<dynamic>()));

        // Act
        var cut = Render<ChannelUsersList>(parameters => parameters
            .Add(p => p.Users, users)
            .Add(p => p.ChannelName, "general")
            .Add(p => p.Username, "Alice")
            .Add(p => p.CanModifyChannel, true));

        cut.WaitForState(() => cut.Markup.Contains("Bob"), TimeSpan.FromSeconds(2));
        cut.Render();

        // Assert - Devrait avoir 1 seul bouton mute (pour Bob, pas pour Alice)
        var muteButtons = cut.FindAll("button.btn-mute");
        Assert.Single(muteButtons);
    }

    [Fact]
    public async Task MuteButton_WhenClicked_ShouldCallMuteEndpoint()
    {
        // Arrange
        var users = new List<User>
        {
            new() { UserId = "user1", Username = "Alice" },
            new() { UserId = "user2", Username = "Bob" }
        };

        _mockHttp
            .When(HttpMethod.Get, "*/api/channels/general/muted-users")
            .Respond(System.Net.HttpStatusCode.OK,
                JsonContent.Create(new List<dynamic>()));

        var postMuteRequest = _mockHttp
            .When(HttpMethod.Post, "*/api/channels/general/muted-users/user1")
            .Respond(System.Net.HttpStatusCode.OK, JsonContent.Create(new
            {
                channelName = "general",
                userId = "user1",
                username = "Alice",
                mutedAt = DateTime.UtcNow
            }));

        var cut = Render<ChannelUsersList>(parameters => parameters
            .Add(p => p.Users, users)
            .Add(p => p.ChannelName, "general")
            .Add(p => p.Username, "Charlie")
            .Add(p => p.CanModifyChannel, true));

        cut.WaitForState(() => cut.Markup.Contains("Alice"), TimeSpan.FromSeconds(2));
        cut.Render();

        // Act
        var muteButton = await cut.InvokeAsync(() => cut.Find("button.btn-mute"));
        await cut.InvokeAsync(() => muteButton.Click());

        // Attendre la requête
        await Task.Delay(500);
        cut.Render();

        // Assert
        Assert.Equal(1, _mockHttp.GetMatchCount(postMuteRequest));
        Assert.Contains("a été rendu muet", cut.Markup);
    }

    [Fact]
    public async Task UnmuteButton_WhenClicked_ShouldCallUnmuteEndpoint()
    {
        // Arrange
        var channelName = Guid.NewGuid().ToString();
        var users = new List<User>
        {
            new() { UserId = "user3", Username = "Alice" },
            new() { UserId = "user4", Username = "Bob" }
        };

        var mutedUsersData = new List<dynamic>
        {
            new { userId = "user3", username = "Alice", mutedAt = DateTime.UtcNow }
        };

        _mockHttp
            .When(HttpMethod.Get, $"*/api/channels/{channelName}/muted-users")
            .Respond(System.Net.HttpStatusCode.OK, JsonContent.Create(mutedUsersData));

        var deleteUnmuteRequest = _mockHttp
            .When(HttpMethod.Delete, $"*/api/channels/{channelName}/muted-users/user3")
            .Respond(System.Net.HttpStatusCode.OK, JsonContent.Create(new
            {
                channelName,
                userId = "user3",
                username = "Alice"
            }));

        var cut = Render<ChannelUsersList>(parameters => parameters
            .Add(p => p.Users, users)
            .Add(p => p.ChannelName, channelName)
            .Add(p => p.Username, "Charlie")
            .Add(p => p.CanModifyChannel, true));

        cut.WaitForState(() => cut.Markup.Contains("btn-unmute"), TimeSpan.FromSeconds(2));
        cut.Render();

        // Act
        var unmuteButton = await cut.InvokeAsync(() => cut.Find("button.btn-unmute"));
        await cut.InvokeAsync(() => unmuteButton.Click());

        // Attendre la requête
        await Task.Delay(500);
        cut.Render();

        // Assert
        Assert.Equal(1, _mockHttp.GetMatchCount(deleteUnmuteRequest));
        Assert.Contains("peut à nouveau parler", cut.Markup);
    }

    [Fact]
    public async Task MuteButton_WhenError_ShouldDisplayErrorMessage()
    {
        // Arrange
        var users = new List<User>
        {
            new() { UserId = "user1", Username = "Alice" }
        };

        _mockHttp
            .When(HttpMethod.Get, "*/api/channels/general/muted-users")
            .Respond(System.Net.HttpStatusCode.OK,
                JsonContent.Create(new List<dynamic>()));

        _mockHttp
            .When(HttpMethod.Post, "*/api/channels/general/muted-users/user1")
            .Respond(System.Net.HttpStatusCode.BadRequest, JsonContent.Create(new
            {
                error = "user_already_muted"
            }));

        var cut = Render<ChannelUsersList>(parameters => parameters
            .Add(p => p.Users, users)
            .Add(p => p.ChannelName, "general")
            .Add(p => p.Username, "Charlie")
            .Add(p => p.CanModifyChannel, true));

        cut.WaitForState(() => cut.Markup.Contains("Alice"), TimeSpan.FromSeconds(2));
        cut.Render();

        // Act
        var muteButton = await cut.InvokeAsync(() => cut.Find("button.btn-mute"));
        await cut.InvokeAsync(() => muteButton.Click());

        // Attendre la requête
        await Task.Delay(500);
        cut.Render();

        // Assert
        Assert.Contains("Impossible de rendre l'utilisateur muet", cut.Markup);
    }
}