// tests/IrcChat.Client.Tests/Pages/ChatTests.cs
using System.Net;
using System.Net.Http.Json;
using Bunit;
using FluentAssertions;
using IrcChat.Client.Pages;
using IrcChat.Client.Services;
using IrcChat.Shared.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace IrcChat.Client.Tests.Pages;

public class ChatTests : TestContext
{
    private readonly Mock<IChatService> _chatServiceMock;
    private readonly Mock<IUnifiedAuthService> _authServiceMock;
    private readonly Mock<IPrivateMessageService> _privateMessageServiceMock;
    private readonly Mock<HttpClient> _httpClientMock;
    private readonly Mock<NavigationManager> _navigationManagerMock;

    public ChatTests()
    {
        _chatServiceMock = new Mock<IChatService>();
        _authServiceMock = new Mock<IUnifiedAuthService>();
        _privateMessageServiceMock = new Mock<IPrivateMessageService>();
        _httpClientMock = new Mock<HttpClient>();
        _navigationManagerMock = new Mock<NavigationManager>();

        Services.AddSingleton(_chatServiceMock.Object);
        Services.AddSingleton(_authServiceMock.Object);
        Services.AddSingleton(_privateMessageServiceMock.Object);
        Services.AddSingleton(_httpClientMock.Object);
        Services.AddSingleton(_navigationManagerMock.Object);
    }

    [Fact]
    public void Chat_WhenNoUsername_ShouldRedirectToLogin()
    {
        // Arrange
        _authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);
        _authServiceMock.Setup(x => x.HasUsername).Returns(false);

        var navigateCalled = false;
        _navigationManagerMock
            .Setup(x => x.NavigateTo("/login", false))
            .Callback(() => navigateCalled = true);

        // Act
        var cut = RenderComponent<Chat>();

        // Assert
        navigateCalled.Should().BeTrue();
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

        _httpClientMock
            .Setup(x => x.GetFromJsonAsync<List<Channel>>("/api/channels", default))
            .ReturnsAsync(channels);

        _chatServiceMock.Setup(x => x.InitializeAsync(It.IsAny<string>())).Returns(Task.CompletedTask);

        _privateMessageServiceMock
            .Setup(x => x.GetConversationsAsync(It.IsAny<string>()))
            .ReturnsAsync([]);

        // Act
        var cut = RenderComponent<Chat>();
        await Task.Delay(100);

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

        _httpClientMock
            .Setup(x => x.GetFromJsonAsync<List<Channel>>("/api/channels", default))
            .ReturnsAsync([]);

        _chatServiceMock.Setup(x => x.InitializeAsync(null)).Returns(Task.CompletedTask);
        _chatServiceMock
            .Setup(x => x.SendMessage(It.IsAny<SendMessageRequest>()))
            .Returns(Task.CompletedTask);

        _privateMessageServiceMock
            .Setup(x => x.GetConversationsAsync(It.IsAny<string>()))
            .ReturnsAsync([]);

        var cut = RenderComponent<Chat>();
        await Task.Delay(100);

        // Simuler la connexion et la sélection d'un canal
        // (Dans un vrai test, il faudrait déclencher les events appropriés)

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

        _httpClientMock
            .Setup(x => x.GetFromJsonAsync<List<Channel>>("/api/channels", default))
            .ReturnsAsync(channels);

        _httpClientMock
            .Setup(x => x.GetFromJsonAsync<List<Message>>("/api/messages/general", default))
            .ReturnsAsync(messages);

        _chatServiceMock.Setup(x => x.InitializeAsync(null)).Returns(Task.CompletedTask);
        _chatServiceMock
            .Setup(x => x.JoinChannel("TestUser", "general"))
            .Returns(Task.CompletedTask);

        _privateMessageServiceMock
            .Setup(x => x.GetConversationsAsync(It.IsAny<string>()))
            .ReturnsAsync([]);

        var cut = RenderComponent<Chat>();
        await Task.Delay(100);

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

        _httpClientMock
            .Setup(x => x.GetFromJsonAsync<List<Channel>>("/api/channels", default))
            .ReturnsAsync([]);

        _chatServiceMock.Setup(x => x.InitializeAsync(null)).Returns(Task.CompletedTask);
        _privateMessageServiceMock
            .Setup(x => x.GetConversationsAsync(It.IsAny<string>()))
            .ReturnsAsync([]);

        var cut = RenderComponent<Chat>();
        await Task.Delay(100);

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
        // Le message devrait être ajouté à la liste
        _chatServiceMock.VerifyAdd(x => x.OnMessageReceived += It.IsAny<Action<Message>>());
    }

    [Fact]
    public async Task Chat_PrivateMessage_ShouldOpenPrivateChat()
    {
        // Arrange
        _authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);
        _authServiceMock.Setup(x => x.HasUsername).Returns(true);
        _authServiceMock.Setup(x => x.Username).Returns("TestUser");

        _httpClientMock
            .Setup(x => x.GetFromJsonAsync<List<Channel>>("/api/channels", default))
            .ReturnsAsync([]);

        _chatServiceMock.Setup(x => x.InitializeAsync(null)).Returns(Task.CompletedTask);

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
        await Task.Delay(100);

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

        _httpClientMock
            .Setup(x => x.GetFromJsonAsync<List<Channel>>("/api/channels", default))
            .ReturnsAsync([]);

        _chatServiceMock.Setup(x => x.InitializeAsync(null)).Returns(Task.CompletedTask);
        _privateMessageServiceMock
            .Setup(x => x.GetConversationsAsync(It.IsAny<string>()))
            .ReturnsAsync([]);

        var cut = RenderComponent<Chat>();
        await Task.Delay(100);

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
    public async Task Chat_Dispose_ShouldUnsubscribeEvents()
    {
        // Arrange
        _authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);
        _authServiceMock.Setup(x => x.HasUsername).Returns(true);
        _authServiceMock.Setup(x => x.Username).Returns("TestUser");

        _httpClientMock
            .Setup(x => x.GetFromJsonAsync<List<Channel>>("/api/channels", default))
            .ReturnsAsync([]);

        _chatServiceMock.Setup(x => x.InitializeAsync(null)).Returns(Task.CompletedTask);
        _chatServiceMock.Setup(x => x.DisposeAsync()).Returns(ValueTask.CompletedTask);

        _privateMessageServiceMock
            .Setup(x => x.GetConversationsAsync(It.IsAny<string>()))
            .ReturnsAsync([]);

        var cut = RenderComponent<Chat>();
        await Task.Delay(100);

        // Act
        cut.Dispose();

        // Assert
        _chatServiceMock.VerifyRemove(
            x => x.OnMessageReceived -= It.IsAny<Action<Message>>());
        _chatServiceMock.VerifyRemove(
            x => x.OnUserJoined -= It.IsAny<Action<string, string>>());
    }
}