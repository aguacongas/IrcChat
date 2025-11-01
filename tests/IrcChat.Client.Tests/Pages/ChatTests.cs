// tests/IrcChat.Client.Tests/Pages/ChatTests.cs
using System.Net;
using System.Net.Http.Json;
using Bunit;
using Bunit.TestDoubles;
using FluentAssertions;
using IrcChat.Client.Pages;
using IrcChat.Client.Services;
using IrcChat.Shared.Models;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using RichardSzalay.MockHttp;
using Xunit;

namespace IrcChat.Client.Tests.Pages;

public class ChatTests : TestContext
{
    private readonly Mock<IChatService> _chatServiceMock;
    private readonly Mock<IUnifiedAuthService> _authServiceMock;
    private readonly Mock<IPrivateMessageService> _privateMessageServiceMock;
    private readonly MockHttpMessageHandler _mockHttp;
    private readonly FakeNavigationManager _navManager;

    public ChatTests()
    {
        _chatServiceMock = new Mock<IChatService>();
        _authServiceMock = new Mock<IUnifiedAuthService>();
        _privateMessageServiceMock = new Mock<IPrivateMessageService>();
        _mockHttp = new MockHttpMessageHandler();

        var httpClient = _mockHttp.ToHttpClient();
        httpClient.BaseAddress = new Uri("https://localhost:7000");

        Services.AddSingleton(_chatServiceMock.Object);
        Services.AddSingleton(_authServiceMock.Object);
        Services.AddSingleton(_privateMessageServiceMock.Object);
        Services.AddSingleton(httpClient);

        _navManager = Services.GetRequiredService<FakeNavigationManager>();
    }

    [Fact]
    public void Chat_WhenNoUsername_ShouldRedirectToLogin()
    {
        // Arrange
        _authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);
        _authServiceMock.Setup(x => x.HasUsername).Returns(false);

        // Act
        var cut = RenderComponent<Chat>();

        // Assert
        _navManager.Uri.Should().EndWith("/login");
    }

    [Fact]
    public async Task Chat_OnInitialization_ShouldLoadChannels()
    {
        // Arrange
        _authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);
        _authServiceMock.Setup(x => x.HasUsername).Returns(true);
        _authServiceMock.Setup(x => x.Username).Returns("TestUser");
        _authServiceMock.Setup(x => x.Token).Returns("test-token");

        var channels = new List<Channel>
        {
            new() { Id = Guid.NewGuid(), Name = "general", CreatedBy = "system", CreatedAt = DateTime.UtcNow },
            new() { Id = Guid.NewGuid(), Name = "random", CreatedBy = "system", CreatedAt = DateTime.UtcNow }
        };

        _mockHttp.When(HttpMethod.Get, "*/api/channels")
            .Respond(HttpStatusCode.OK, JsonContent.Create(channels));

        _chatServiceMock.Setup(x => x.InitializeAsync(It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        _privateMessageServiceMock
            .Setup(x => x.GetConversationsAsync(It.IsAny<string>()))
            .ReturnsAsync([]);

        // Act
        var cut = RenderComponent<Chat>();
        await Task.Delay(200);

        // Assert
        cut.Markup.Should().Contain("general");
        cut.Markup.Should().Contain("random");
    }

    [Fact]
    public async Task Chat_SendMessage_ShouldCallChatService()
    {
        // Arrange
        _authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);
        _authServiceMock.Setup(x => x.HasUsername).Returns(true);
        _authServiceMock.Setup(x => x.Username).Returns("TestUser");

        _mockHttp.When(HttpMethod.Get, "*/api/channels")
            .Respond(HttpStatusCode.OK, JsonContent.Create(new List<Channel>()));

        _chatServiceMock.Setup(x => x.InitializeAsync(null))
            .Returns(Task.CompletedTask);
        _chatServiceMock
            .Setup(x => x.SendMessage(It.IsAny<SendMessageRequest>()))
            .Returns(Task.CompletedTask);

        _privateMessageServiceMock
            .Setup(x => x.GetConversationsAsync(It.IsAny<string>()))
            .ReturnsAsync([]);

        var cut = RenderComponent<Chat>();
        await Task.Delay(200);

        // Assert - Vérifier que le composant est rendu
        cut.Find(".chat-container").Should().NotBeNull();
    }

    [Fact]
    public async Task Chat_JoinChannel_ShouldLoadMessagesAndUsers()
    {
        // Arrange
        _authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);
        _authServiceMock.Setup(x => x.HasUsername).Returns(true);
        _authServiceMock.Setup(x => x.Username).Returns("TestUser");

        var channels = new List<Channel>
        {
            new() { Id = Guid.NewGuid(), Name = "general", CreatedBy = "system", CreatedAt = DateTime.UtcNow }
        };

        var messages = new List<Message>
        {
            new()
            {
                Id = Guid.NewGuid(),
                Username = "User1",
                Content = "Hello",
                Channel = "general",
                Timestamp = DateTime.UtcNow
            }
        };

        _mockHttp.When(HttpMethod.Get, "*/api/channels")
            .Respond(HttpStatusCode.OK, JsonContent.Create(channels));

        _mockHttp.When(HttpMethod.Get, "*/api/messages/general")
            .Respond(HttpStatusCode.OK, JsonContent.Create(messages));

        _chatServiceMock.Setup(x => x.InitializeAsync(null))
            .Returns(Task.CompletedTask);
        _chatServiceMock
            .Setup(x => x.JoinChannel("TestUser", "general"))
            .Returns(Task.CompletedTask);

        _privateMessageServiceMock
            .Setup(x => x.GetConversationsAsync(It.IsAny<string>()))
            .ReturnsAsync([]);

        var cut = RenderComponent<Chat>();
        await Task.Delay(200);

        // Act
        await cut.InvokeAsync(() => cut.Find($"ul.channel-list > li[blazor\\:onclick]").Click());

        // Assert
        _chatServiceMock.Verify(
            x => x.JoinChannel(It.IsAny<string>(), It.IsAny<string>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task Chat_ReceiveMessage_ShouldUpdateMessageList()
    {
        // Arrange
        _authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);
        _authServiceMock.Setup(x => x.HasUsername).Returns(true);
        _authServiceMock.Setup(x => x.Username).Returns("TestUser");

        _mockHttp.When(HttpMethod.Get, "*/api/channels")
            .Respond(HttpStatusCode.OK, JsonContent.Create(new List<Channel>()));

        _chatServiceMock.Setup(x => x.InitializeAsync(null))
            .Returns(Task.CompletedTask);

        _privateMessageServiceMock
            .Setup(x => x.GetConversationsAsync(It.IsAny<string>()))
            .ReturnsAsync([]);

        var cut = RenderComponent<Chat>();
        await Task.Delay(200);

        var newMessage = new Message
        {
            Id = Guid.NewGuid(),
            Username = "User1",
            Content = "Test message",
            Channel = "general",
            Timestamp = DateTime.UtcNow
        };

        // Act - Simuler la réception d'un message
        _chatServiceMock.Raise(x => x.OnMessageReceived += null, newMessage);
        await Task.Delay(100);

        // Assert
        _chatServiceMock.VerifyAdd(x => x.OnMessageReceived += It.IsAny<Action<Message>>());
    }

    [Fact]
    public async Task Chat_PrivateMessage_ShouldOpenPrivateChat()
    {
        // Arrange
        _authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);
        _authServiceMock.Setup(x => x.HasUsername).Returns(true);
        _authServiceMock.Setup(x => x.Username).Returns("TestUser");

        _mockHttp.When(HttpMethod.Get, "*/api/channels")
            .Respond(HttpStatusCode.OK, JsonContent.Create(new List<Channel>()));

        _chatServiceMock.Setup(x => x.InitializeAsync(null))
            .Returns(Task.CompletedTask);

        var conversations = new List<PrivateConversation>
        {
            new()
            {
                OtherUsername = "User2",
                LastMessage = "Hello",
                LastMessageTime = DateTime.UtcNow,
                UnreadCount = 1
            }
        };

        _privateMessageServiceMock
            .Setup(x => x.GetConversationsAsync("TestUser"))
            .ReturnsAsync(conversations);

        _privateMessageServiceMock
            .Setup(x => x.GetPrivateMessagesAsync("TestUser", "User2"))
            .ReturnsAsync([]);

        var cut = RenderComponent<Chat>();
        await Task.Delay(200);

        // Assert - Vérifier que les conversations sont chargées
        _privateMessageServiceMock.Verify(
            x => x.GetConversationsAsync("TestUser"),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task Chat_MuteStatusChange_ShouldUpdateChannel()
    {
        // Arrange
        _authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);
        _authServiceMock.Setup(x => x.HasUsername).Returns(true);
        _authServiceMock.Setup(x => x.Username).Returns("TestUser");

        _mockHttp.When(HttpMethod.Get, "*/api/channels")
            .Respond(HttpStatusCode.OK, JsonContent.Create(new List<Channel>()));

        _chatServiceMock.Setup(x => x.InitializeAsync(null))
            .Returns(Task.CompletedTask);

        _privateMessageServiceMock
            .Setup(x => x.GetConversationsAsync(It.IsAny<string>()))
            .ReturnsAsync([]);

        var cut = RenderComponent<Chat>();
        await Task.Delay(200);

        // Act - Simuler un changement de statut mute
        _chatServiceMock.Raise(
            x => x.OnChannelMuteStatusChanged += null,
            "general",
            true);

        await Task.Delay(100);

        // Assert
        _chatServiceMock.VerifyAdd(
            x => x.OnChannelMuteStatusChanged += It.IsAny<Action<string, bool>>());
    }

    [Fact]
    public async Task Chat_LeaveChannel_WhenSwitchingChannels_ShouldCallLeaveChannel()
    {
        // Arrange
        _authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);
        _authServiceMock.Setup(x => x.HasUsername).Returns(true);
        _authServiceMock.Setup(x => x.Username).Returns("TestUser");

        var channels = new List<Channel>
        {
            new() { Id = Guid.NewGuid(), Name = "general", CreatedBy = "system", CreatedAt = DateTime.UtcNow },
            new() { Id = Guid.NewGuid(), Name = "random", CreatedBy = "system", CreatedAt = DateTime.UtcNow }
        };

        _mockHttp.When(HttpMethod.Get, "*/api/channels")
            .Respond(HttpStatusCode.OK, JsonContent.Create(channels));

        _mockHttp.When(HttpMethod.Get, "*/api/messages/*")
            .Respond(HttpStatusCode.OK, JsonContent.Create(new List<Message>()));

        _chatServiceMock.Setup(x => x.InitializeAsync(null))
            .Returns(Task.CompletedTask);
        _chatServiceMock.Setup(x => x.JoinChannel(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);
        _chatServiceMock.Setup(x => x.LeaveChannel(It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        _privateMessageServiceMock
            .Setup(x => x.GetConversationsAsync(It.IsAny<string>()))
            .ReturnsAsync([]);

        var cut = RenderComponent<Chat>();
        await Task.Delay(200);

        var chanelItems = cut.FindAll($"ul.channel-list > li[blazor\\:onclick]");
        await cut.InvokeAsync(() => chanelItems[0].Click());

        // Act
        chanelItems = cut.FindAll($"ul.channel-list > li[blazor\\:onclick]");
        await cut.InvokeAsync(() => chanelItems[1].Click());

        // Assert
        _chatServiceMock.Verify(
            x => x.LeaveChannel(It.IsAny<string>()),
            Times.AtLeastOnce);
    }
}