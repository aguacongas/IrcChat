// tests/IrcChat.Client.Tests/Components/ChatAreaTests.cs
using Bunit;
using IrcChat.Client.Components;
using IrcChat.Client.Services;
using IrcChat.Shared.Models;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using RichardSzalay.MockHttp;
using Xunit;

namespace IrcChat.Client.Tests.Components;

public class ChatAreaTests : TestContext
{
    private readonly MockHttpMessageHandler _mockHttp;
    private readonly Mock<IIgnoredUsersService> _ignoredUsersServiceMock;

    public ChatAreaTests()
    {
        _mockHttp = new MockHttpMessageHandler();
        _ignoredUsersServiceMock = new Mock<IIgnoredUsersService>();
        _ignoredUsersServiceMock.Setup(x => x.InitializeAsync())
            .Returns(Task.CompletedTask);

        var httpClient = _mockHttp.ToHttpClient();
        httpClient.BaseAddress = new Uri("https://localhost:7000");
        Services.AddSingleton(httpClient);
        Services.AddSingleton(_ignoredUsersServiceMock.Object);

        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public void ChatArea_WhenNoChannel_ShouldShowWelcomeMessage()
    {
        // Arrange & Act
        var cut = RenderComponent<ChatArea>(parameters => parameters
            .Add(p => p.Username, "TestUser")
            .Add(p => p.CurrentChannel, "")
            .Add(p => p.IsConnected, false)
            .Add(p => p.UsersListOpen, false));

        // Assert
        Assert.Contains("Bienvenue TestUser!", cut.Markup);
        Assert.Contains("Créez ou rejoignez un salon", cut.Markup);
    }

    [Fact]
    public void ChatArea_WithChannel_ShouldShowChatHeader()
    {
        // Arrange & Act
        var cut = RenderComponent<ChatArea>(parameters => parameters
            .Add(p => p.Username, "TestUser")
            .Add(p => p.CurrentChannel, "general")
            .Add(p => p.IsConnected, true)
            .Add(p => p.UsersListOpen, false));

        // Assert
        Assert.Contains("#general", cut.Markup);
        Assert.Contains("chat-header", cut.Markup);
    }

    [Fact]
    public void ChatArea_WhenConnected_ShouldShowConnectedStatus()
    {
        // Arrange & Act
        var cut = RenderComponent<ChatArea>(parameters => parameters
            .Add(p => p.Username, "TestUser")
            .Add(p => p.CurrentChannel, "general")
            .Add(p => p.IsConnected, true)
            .Add(p => p.UsersListOpen, false));

        // Assert
        Assert.Contains("● Connecté", cut.Markup);
        Assert.Contains("connected", cut.Markup);
    }

    [Fact]
    public void ChatArea_WhenDisconnected_ShouldShowDisconnectedStatus()
    {
        // Arrange & Act
        var cut = RenderComponent<ChatArea>(parameters => parameters
            .Add(p => p.Username, "TestUser")
            .Add(p => p.CurrentChannel, "general")
            .Add(p => p.IsConnected, false)
            .Add(p => p.UsersListOpen, false));

        // Assert
        Assert.Contains("○ Déconnecté", cut.Markup);
        Assert.Contains("disconnected", cut.Markup);
    }

    [Fact]
    public void ChatArea_WithUsersListOpen_ShouldShowVisibleClass()
    {
        // Arrange & Act
        var cut = RenderComponent<ChatArea>(parameters => parameters
            .Add(p => p.Username, "TestUser")
            .Add(p => p.CurrentChannel, "general")
            .Add(p => p.IsConnected, true)
            .Add(p => p.UsersListOpen, true)
            .Add(p => p.Users,
            [
                new() { Username = "User1", ConnectedAt = DateTime.UtcNow }
            ]));

        // Assert
        Assert.Contains("users-open", cut.Markup);
        Assert.Contains("visible", cut.Markup);
    }

    [Fact]
    public void ChatArea_WithUsersListClosed_ShouldShowHiddenClass()
    {
        // Arrange & Act
        var cut = RenderComponent<ChatArea>(parameters => parameters
            .Add(p => p.Username, "TestUser")
            .Add(p => p.CurrentChannel, "general")
            .Add(p => p.IsConnected, true)
            .Add(p => p.UsersListOpen, false)
            .Add(p => p.Users, []));

        cut.Render();

        // Assert
        Assert.Contains("users-closed", cut.Markup);
        Assert.Contains("hidden", cut.Markup);
    }

    [Fact]
    public void ChatArea_ShouldRenderUsersListToggleButton()
    {
        // Arrange & Act
        var cut = RenderComponent<ChatArea>(parameters => parameters
            .Add(p => p.Username, "TestUser")
            .Add(p => p.CurrentChannel, "general")
            .Add(p => p.IsConnected, true)
            .Add(p => p.UsersListOpen, false)
            .Add(p => p.Users,
            [
                new() { Username = "User1", ConnectedAt = DateTime.UtcNow },
                new() { Username = "User2", ConnectedAt = DateTime.UtcNow }
            ]));

        // Assert
        Assert.Contains("users-toggle-btn", cut.Markup);
        Assert.Contains("2", cut.Markup); // UserCount
    }

    [Fact]
    public void ChatArea_ShouldRenderChannelMuteButton()
    {
        // Arrange & Act
        var cut = RenderComponent<ChatArea>(parameters => parameters
            .Add(p => p.Username, "TestUser")
            .Add(p => p.CurrentChannel, "general")
            .Add(p => p.IsConnected, true)
            .Add(p => p.UsersListOpen, false)
            .Add(p => p.CanManage, true));

        // Assert
        Assert.Contains("channel-mute-control", cut.Markup);
    }

    [Fact]
    public void ChatArea_ShouldRenderChannelDeleteButton()
    {
        // Arrange & Act
        var cut = RenderComponent<ChatArea>(parameters => parameters
            .Add(p => p.Username, "TestUser")
            .Add(p => p.CurrentChannel, "general")
            .Add(p => p.IsConnected, true)
            .Add(p => p.UsersListOpen, false)
            .Add(p => p.CanManage, true));

        // Assert
        Assert.Contains("channel-delete-control", cut.Markup);
    }

    [Fact]
    public void ChatArea_WhenCannotManage_ShouldNotShowManagementButtons()
    {
        // Arrange & Act
        var cut = RenderComponent<ChatArea>(parameters => parameters
            .Add(p => p.Username, "TestUser")
            .Add(p => p.CurrentChannel, "general")
            .Add(p => p.IsConnected, true)
            .Add(p => p.UsersListOpen, false)
            .Add(p => p.CanManage, false));

        // Assert
        Assert.DoesNotContain("mute-btn", cut.Markup);
        Assert.DoesNotContain("delete-btn", cut.Markup);
    }

    [Fact]
    public void ChatArea_ShouldRenderMessageList()
    {
        // Arrange
        var messages = new List<Message>
        {
            new()
            {
                Id = Guid.NewGuid(),
                Username = "User1",
                Content = "Test message",
                Channel = "general",
                Timestamp = DateTime.UtcNow
            }
        };

        // Act
        var cut = RenderComponent<ChatArea>(parameters => parameters
            .Add(p => p.Username, "TestUser")
            .Add(p => p.CurrentChannel, "general")
            .Add(p => p.IsConnected, true)
            .Add(p => p.Messages, messages)
            .Add(p => p.UsersListOpen, false));

        // Assert
        Assert.Contains("Test message", cut.Markup);
    }

    [Fact]
    public void ChatArea_ShouldRenderMessageInput()
    {
        // Arrange & Act
        var cut = RenderComponent<ChatArea>(parameters => parameters
            .Add(p => p.Username, "TestUser")
            .Add(p => p.CurrentChannel, "general")
            .Add(p => p.IsConnected, true)
            .Add(p => p.UsersListOpen, false));

        // Assert
        Assert.Contains("input-area", cut.Markup);
        Assert.Contains("placeholder=\"Tapez votre message...\"", cut.Markup);
    }

    [Fact]
    public void ChatArea_ShouldRenderChannelUsersList()
    {
        // Arrange
        var users = new List<User>
        {
            new() { Username = "User1", ConnectedAt = DateTime.UtcNow },
            new() { Username = "User2", ConnectedAt = DateTime.UtcNow }
        };

        // Act
        var cut = RenderComponent<ChatArea>(parameters => parameters
            .Add(p => p.Username, "TestUser")
            .Add(p => p.CurrentChannel, "general")
            .Add(p => p.IsConnected, true)
            .Add(p => p.Users, users)
            .Add(p => p.UsersListOpen, true));

        // Assert
        Assert.Contains("User1", cut.Markup);
        Assert.Contains("User2", cut.Markup);
    }

    [Fact]
    public async Task ChatArea_UsersListToggleButton_WhenClicked_ShouldInvokeCallback()
    {
        // Arrange
        var callbackInvoked = false;
        var newState = false;

        var cut = RenderComponent<ChatArea>(parameters => parameters
            .Add(p => p.Username, "TestUser")
            .Add(p => p.CurrentChannel, "general")
            .Add(p => p.IsConnected, true)
            .Add(p => p.UsersListOpen, false)
            .Add(p => p.Users,
            [
                new() { Username = "User1", ConnectedAt = DateTime.UtcNow }
            ])
            .Add(p => p.OnUsersListToggle, state =>
            {
                callbackInvoked = true;
                newState = state;
            }));

        // Act
        var button = cut.Find(".users-toggle-btn");
        await cut.InvokeAsync(() => button.Click());

        // Assert
        Assert.True(callbackInvoked);
        Assert.True(newState);
    }

    [Fact]
    public async Task ChatArea_MessageInput_WhenMessageSent_ShouldInvokeCallback()
    {
        // Arrange
        var messageSent = "";

        var cut = RenderComponent<ChatArea>(parameters => parameters
            .Add(p => p.Username, "TestUser")
            .Add(p => p.CurrentChannel, "general")
            .Add(p => p.IsConnected, true)
            .Add(p => p.UsersListOpen, false)
            .Add(p => p.OnMessageSent, message => messageSent = message));

        // Act
        var input = cut.Find(".input-area input");
        var button = cut.Find(".input-area button");

        await cut.InvokeAsync(() => input.Input("Hello World"));
        await cut.InvokeAsync(() => button.Click());

        // Assert
        Assert.Equal("Hello World", messageSent);
    }

    [Fact]
    public void ChatArea_WithMutedChannel_ShouldPassMutedState()
    {
        // Arrange & Act
        var cut = RenderComponent<ChatArea>(parameters => parameters
            .Add(p => p.Username, "TestUser")
            .Add(p => p.CurrentChannel, "general")
            .Add(p => p.IsConnected, true)
            .Add(p => p.IsMuted, true)
            .Add(p => p.CanManage, true)
            .Add(p => p.UsersListOpen, false));

        // Assert
        Assert.Contains("mute-btn", cut.Markup);
        Assert.Contains("muted", cut.Markup);
    }

    [Fact]
    public void ChatArea_WithEmptyMessages_ShouldStillRenderMessageList()
    {
        // Arrange & Act
        var cut = RenderComponent<ChatArea>(parameters => parameters
            .Add(p => p.Username, "TestUser")
            .Add(p => p.CurrentChannel, "general")
            .Add(p => p.IsConnected, true)
            .Add(p => p.Messages, [])
            .Add(p => p.UsersListOpen, false));

        // Assert
        Assert.Contains("messages", cut.Markup);
    }

    [Fact]
    public void ChatArea_WithEmptyUsers_ShouldShowZeroCount()
    {
        // Arrange & Act
        var cut = RenderComponent<ChatArea>(parameters => parameters
            .Add(p => p.Username, "TestUser")
            .Add(p => p.CurrentChannel, "general")
            .Add(p => p.IsConnected, true)
            .Add(p => p.Users, [])
            .Add(p => p.UsersListOpen, false));

        // Assert
        Assert.Contains("0", cut.Markup); // UserCount devrait être 0
    }

    [Fact]
    public async Task ChatArea_ChannelDeleteButton_WhenClicked_ShouldInvokeCallback()
    {
        // Arrange
        _mockHttp.When(HttpMethod.Delete, "*/api/channels/general")
            .Respond(System.Net.HttpStatusCode.OK);

        var deletedChannel = "";

        var cut = RenderComponent<ChatArea>(parameters => parameters
            .Add(p => p.Username, "TestUser")
            .Add(p => p.CurrentChannel, "general")
            .Add(p => p.IsConnected, true)
            .Add(p => p.CanManage, true)
            .Add(p => p.UsersListOpen, false)
            .Add(p => p.OnChannelDeleted, channel => deletedChannel = channel));

        // Act - Cliquer sur le bouton de suppression puis confirmer
        var deleteButton = cut.Find(".delete-btn");
        await cut.InvokeAsync(() => deleteButton.Click());
        await Task.Delay(100);

        var confirmButton = cut.Find(".btn-danger");
        await cut.InvokeAsync(() => confirmButton.Click());
        await Task.Delay(100);

        // Assert
        Assert.Equal("general", deletedChannel);
    }

    [Fact]
    public void ChatArea_HeaderRight_ShouldContainAllButtons()
    {
        // Arrange & Act
        var cut = RenderComponent<ChatArea>(parameters => parameters
            .Add(p => p.Username, "TestUser")
            .Add(p => p.CurrentChannel, "general")
            .Add(p => p.IsConnected, true)
            .Add(p => p.CanManage, true)
            .Add(p => p.UsersListOpen, false)
            .Add(p => p.Users,
            [
                new() { Username = "User1", ConnectedAt = DateTime.UtcNow }
            ]));

        // Assert
        var headerRight = cut.Find(".header-right");
        Assert.NotNull(headerRight);
        Assert.Contains("users-toggle-btn", cut.Markup);
        Assert.Contains("channel-mute-control", cut.Markup);
        Assert.Contains("channel-delete-control", cut.Markup);
    }

    [Fact]
    public void ChatArea_WhenNoChannel_ShouldNotRenderHeaderOrContent()
    {
        // Arrange & Act
        var cut = RenderComponent<ChatArea>(parameters => parameters
            .Add(p => p.Username, "TestUser")
            .Add(p => p.CurrentChannel, "")
            .Add(p => p.IsConnected, false)
            .Add(p => p.UsersListOpen, false));

        // Assert
        Assert.DoesNotContain("chat-header", cut.Markup);
        Assert.DoesNotContain("chat-content", cut.Markup);
        Assert.Contains("welcome", cut.Markup);
    }
}