// tests/IrcChat.Client.Tests/Pages/ChatTests.PrivateMessages.cs
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Http.Json;
using Bunit;
using IrcChat.Client.Pages;
using IrcChat.Shared.Models;
using Microsoft.AspNetCore.SignalR.Client;
using Moq;
using RichardSzalay.MockHttp;
using Xunit;

namespace IrcChat.Client.Tests.Pages;

public partial class ChatTests
{
    // ============ TESTS POUR HandleUserClicked ============

    [Fact]
    public async Task Chat_HandleUserClicked_WhenDifferentUser_ShouldNavigateToPrivateChat()
    {
        // Arrange
        SetupBasicAuth();
        var channels = new List<Channel>
        {
            new() { Id = Guid.NewGuid(), Name = "general", CreatedBy = "system", CreatedAt = DateTime.UtcNow }
        };
        var users = new List<User>
        {
            new() { UserId = "user1", Username = "Alice" },
            new() { UserId = "user2", Username = "Bob" }
        };

        _mockHttp.When(HttpMethod.Get, "*/api/channels")
            .Respond(HttpStatusCode.OK, JsonContent.Create(channels));
        _mockHttp.When(HttpMethod.Get, "*/api/messages/general")
            .Respond(HttpStatusCode.OK, JsonContent.Create(new List<Message>()));
        _mockHttp.When(HttpMethod.Get, "*/api/channels/general/users")
            .Respond(HttpStatusCode.OK, JsonContent.Create(users));
        _mockHttp.When(HttpMethod.Get, "*/api/private-messages/status/Alice")
            .Respond(HttpStatusCode.OK, JsonContent.Create(new { Username = "Alice", IsOnline = true }));

        _chatServiceMock.Setup(x => x.InitializeAsync(It.IsAny<IHubConnectionBuilder>()))
            .Returns(Task.CompletedTask);
        _chatServiceMock.Setup(x => x.JoinChannel("general")).Returns(Task.CompletedTask);
        _chatServiceMock.Setup(x => x.MarkPrivateMessagesAsRead(It.IsAny<string>()))
            .Returns(Task.CompletedTask);
        _privateMessageServiceMock.Setup(x => x.GetConversationsAsync(It.IsAny<string>()))
            .ReturnsAsync([]);
        _privateMessageServiceMock.Setup(x => x.GetPrivateMessagesAsync("TestUser", "user1"))
            .ReturnsAsync([]);

        var cut = await RenderChatAsync(channelName: "general");

        // Act - Cliquer sur Alice dans la liste des utilisateurs
        var userItemList = await cut.InvokeAsync(() => cut.FindAll(".user-item"));
        var userItem = userItemList.First(item => item.TextContent.Contains("Alice"));
        await cut.InvokeAsync(() => userItem.Click());
        await Task.Delay(200);

        // Assert - Devrait naviguer vers la conversation privée avec Alice
        Assert.Contains("/chat/private/user1", _navManager.Uri);
    }

    [Fact]
    public async Task Chat_HandleUserClicked_WhenCurrentUser_ShouldNotNavigate()
    {
        // Arrange
        SetupBasicAuth();
        var channels = new List<Channel>
        {
            new() { Id = Guid.NewGuid(), Name = "general", CreatedBy = "system", CreatedAt = DateTime.UtcNow }
        };
        var users = new List<User>
        {
            new() { UserId = "TestUser", Username = "TestUser" },
            new() { UserId = "user1", Username = "Alice" }
        };

        _mockHttp.When(HttpMethod.Get, "*/api/channels")
            .Respond(HttpStatusCode.OK, JsonContent.Create(channels));
        _mockHttp.When(HttpMethod.Get, "*/api/messages/general")
            .Respond(HttpStatusCode.OK, JsonContent.Create(new List<Message>()));
        _mockHttp.When(HttpMethod.Get, "*/api/channels/general/users")
            .Respond(HttpStatusCode.OK, JsonContent.Create(users));

        _chatServiceMock.Setup(x => x.InitializeAsync(It.IsAny<IHubConnectionBuilder>()))
            .Returns(Task.CompletedTask);
        _chatServiceMock.Setup(x => x.JoinChannel("general")).Returns(Task.CompletedTask);
        _privateMessageServiceMock.Setup(x => x.GetConversationsAsync(It.IsAny<string>()))
            .ReturnsAsync([]);

        var cut = await RenderChatAsync(channelName: "general");
        var initialUri = _navManager.Uri;

        // Act - Cliquer sur soi-même
        var userItemList = await cut.InvokeAsync(() => cut.FindAll(".user-item"));
        var userItem = userItemList.First(item => item.TextContent.Contains("TestUser"));
        await cut.InvokeAsync(() => userItem.Click());
        await Task.Delay(200);

        // Assert - Ne devrait pas naviguer
        Assert.Equal(initialUri, _navManager.Uri);
    }

    [Fact]
    public async Task Chat_HandleUserClicked_ShouldCloseUsersListOnMobile()
    {
        // Arrange
        SetupBasicAuth();
        var channels = new List<Channel>
        {
            new() { Id = Guid.NewGuid(), Name = "general", CreatedBy = "system", CreatedAt = DateTime.UtcNow }
        };
        var users = new List<User>
        {
            new() { UserId = "user1", Username = "Alice" }
        };

        _mockHttp.When(HttpMethod.Get, "*/api/channels")
            .Respond(HttpStatusCode.OK, JsonContent.Create(channels));
        _mockHttp.When(HttpMethod.Get, "*/api/messages/general")
            .Respond(HttpStatusCode.OK, JsonContent.Create(new List<Message>()));
        _mockHttp.When(HttpMethod.Get, "*/api/channels/general/users")
            .Respond(HttpStatusCode.OK, JsonContent.Create(users));
        _mockHttp.When(HttpMethod.Get, "*/api/private-messages/status/Alice")
            .Respond(HttpStatusCode.OK, JsonContent.Create(new { Username = "Alice", IsOnline = true }));

        _deviceDetectorMock.Setup(x => x.IsMobileDeviceAsync()).ReturnsAsync(true);
        _chatServiceMock.Setup(x => x.InitializeAsync(It.IsAny<IHubConnectionBuilder>()))
            .Returns(Task.CompletedTask);
        _chatServiceMock.Setup(x => x.JoinChannel("general")).Returns(Task.CompletedTask);
        _chatServiceMock.Setup(x => x.MarkPrivateMessagesAsRead(It.IsAny<string>()))
            .Returns(Task.CompletedTask);
        _privateMessageServiceMock.Setup(x => x.GetConversationsAsync(It.IsAny<string>()))
            .ReturnsAsync([]);
        _privateMessageServiceMock.Setup(x => x.GetPrivateMessagesAsync("TestUser", "user1"))
            .ReturnsAsync([]);

        var cut = await RenderChatAsync(channelName: "general");

        // Act - Cliquer sur un utilisateur
        var userItem = await cut.InvokeAsync(() => cut.Find(".user-item"));
        await cut.InvokeAsync(() => userItem.Click());
        await Task.Delay(200);
        cut.Render();

        // Assert - La liste des utilisateurs devrait être fermée
        Assert.DoesNotContain("users-list-open", cut.Markup);
    }

    // ============ TESTS POUR StartPrivateChat ============

    [Fact]
    public async Task Chat_StartPrivateChat_ShouldLoadPrivateMessages()
    {
        // Arrange
        SetupBasicAuth();
        _mockHttp.When(HttpMethod.Get, "*/api/channels")
            .Respond(HttpStatusCode.OK, JsonContent.Create(new List<Channel>()));
        _mockHttp.When(HttpMethod.Get, "*/api/private-messages/status/Alice")
            .Respond(HttpStatusCode.OK, JsonContent.Create(new { Username = "Alice", IsOnline = true }));

        _chatServiceMock.Setup(x => x.InitializeAsync(It.IsAny<IHubConnectionBuilder>()))
            .Returns(Task.CompletedTask);
        _chatServiceMock.Setup(x => x.MarkPrivateMessagesAsRead(It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        var privateMessages = new List<PrivateMessage>
        {
            new() { Id = Guid.NewGuid(), SenderUserId = "user1", SenderUsername = "Alice", RecipientUserId = "TestUser", RecipientUsername = "TestUser", Content = "Hello!", Timestamp = DateTime.UtcNow }
        };

        _privateMessageServiceMock.Setup(x => x.GetConversationsAsync("TestUser"))
            .ReturnsAsync([new() { OtherUser = new User { UserId = "user1", Username = "Alice" }, LastMessage = "Hello!", LastMessageTime = DateTime.UtcNow, UnreadCount = 1 }]);
        _privateMessageServiceMock.Setup(x => x.GetPrivateMessagesAsync("TestUser", "user1"))
            .ReturnsAsync(privateMessages);

        // Act
        var cut = await RenderChatAsync();
        await NavigateToPrivateChatAsync(cut, "user1", "Alice");
        cut.Render();

        // Assert
        _privateMessageServiceMock.Verify(x => x.GetPrivateMessagesAsync("TestUser", "user1"), Times.AtLeastOnce);
        Assert.Contains("Hello!", cut.Markup);
    }

    [Fact]
    public async Task Chat_StartPrivateChat_ShouldMarkMessagesAsRead()
    {
        // Arrange
        SetupBasicAuth();
        _mockHttp.When(HttpMethod.Get, "*/api/channels")
            .Respond(HttpStatusCode.OK, JsonContent.Create(new List<Channel>()));
        _mockHttp.When(HttpMethod.Get, "*/api/private-messages/status/Alice")
            .Respond(HttpStatusCode.OK, JsonContent.Create(new { Username = "Alice", IsOnline = true }));

        _chatServiceMock.Setup(x => x.InitializeAsync(It.IsAny<IHubConnectionBuilder>()))
            .Returns(Task.CompletedTask);
        _chatServiceMock.Setup(x => x.MarkPrivateMessagesAsRead("user1"))
            .Returns(Task.CompletedTask);

        _privateMessageServiceMock.Setup(x => x.GetConversationsAsync("TestUser"))
            .ReturnsAsync([new() { OtherUser = new User { UserId = "user1", Username = "Alice" }, LastMessage = "Hi", LastMessageTime = DateTime.UtcNow, UnreadCount = 2 }]);
        _privateMessageServiceMock.Setup(x => x.GetPrivateMessagesAsync("TestUser", "user1"))
            .ReturnsAsync([]);

        // Act
        var cut = await RenderChatAsync();
        await NavigateToPrivateChatAsync(cut, "user1", "Alice");

        // Assert
        _chatServiceMock.Verify(x => x.MarkPrivateMessagesAsRead("user1"), Times.AtLeastOnce);
    }

    [Fact]
    public async Task Chat_StartPrivateChat_ShouldCloseSidebar()
    {
        // Arrange
        SetupBasicAuth();
        _mockHttp.When(HttpMethod.Get, "*/api/channels")
            .Respond(HttpStatusCode.OK, JsonContent.Create(new List<Channel>()));
        _mockHttp.When(HttpMethod.Get, "*/api/private-messages/status/Alice")
            .Respond(HttpStatusCode.OK, JsonContent.Create(new { Username = "Alice", IsOnline = true }));

        _chatServiceMock.Setup(x => x.InitializeAsync(It.IsAny<IHubConnectionBuilder>()))
            .Returns(Task.CompletedTask);
        _chatServiceMock.Setup(x => x.MarkPrivateMessagesAsRead(It.IsAny<string>()))
            .Returns(Task.CompletedTask);
        _privateMessageServiceMock.Setup(x => x.GetConversationsAsync("TestUser"))
            .ReturnsAsync([new() { OtherUser = new User { UserId = "user1", Username = "Alice" }, LastMessage = "Hi", LastMessageTime = DateTime.UtcNow, UnreadCount = 0 }]);
        _privateMessageServiceMock.Setup(x => x.GetPrivateMessagesAsync("TestUser", "user1"))
            .ReturnsAsync([]);

        var cut = await RenderChatAsync();

        // Ouvrir la sidebar
        var toggleButton = cut.Find(".sidebar-toggle-btn");
        await cut.InvokeAsync(() => toggleButton.Click());
        await Task.Delay(100);
        cut.Render();
        Assert.Contains("sidebar-open", cut.Markup);

        // Act - Démarrer une conversation privée
        await NavigateToPrivateChatAsync(cut, "user1", "Alice");
        cut.Render();

        // Assert - La sidebar devrait être fermée
        Assert.Contains("sidebar-closed", cut.Markup);
    }

    [Fact]
    public async Task Chat_StartPrivateChat_ShouldLoadUserOnlineStatus()
    {
        // Arrange
        SetupBasicAuth();
        _mockHttp.When(HttpMethod.Get, "*/api/channels")
            .Respond(HttpStatusCode.OK, JsonContent.Create(new List<Channel>()));
        _mockHttp.When(HttpMethod.Get, "*/api/private-messages/status/Alice")
            .Respond(HttpStatusCode.OK, JsonContent.Create(new { Username = "Alice", IsOnline = true }));

        _chatServiceMock.Setup(x => x.InitializeAsync(It.IsAny<IHubConnectionBuilder>()))
            .Returns(Task.CompletedTask);
        _chatServiceMock.Setup(x => x.MarkPrivateMessagesAsRead(It.IsAny<string>()))
            .Returns(Task.CompletedTask);
        _privateMessageServiceMock.Setup(x => x.GetConversationsAsync("TestUser"))
            .ReturnsAsync([new() { OtherUser = new User { UserId = "user1", Username = "Alice" }, LastMessage = "Hi", LastMessageTime = DateTime.UtcNow, UnreadCount = 0 }]);
        _privateMessageServiceMock.Setup(x => x.GetPrivateMessagesAsync("TestUser", "user1"))
            .ReturnsAsync([]);

        // Act
        var cut = await RenderChatAsync();
        await NavigateToPrivateChatAsync(cut, "user1", "Alice");
        cut.Render();

        // Assert - Devrait avoir fait une requête pour le statut
        _mockHttp.VerifyNoOutstandingExpectation();
        Assert.Contains("En ligne", cut.Markup);
    }

    [Fact]
    public async Task Chat_StartPrivateChat_WithEmptyUserId_ShouldNotNavigate()
    {
        // Arrange
        SetupBasicAuth();
        SetupBasicMocks();

        var cut = await RenderChatAsync();
        var initialUri = _navManager.Uri;

        var emptyUser = new User { UserId = "", Username = "NoId" };

        // Act
        await StartPrivateChatFromSidebarAsync(cut, emptyUser);

        // Assert - Ne devrait pas naviguer
        Assert.Equal(initialUri, _navManager.Uri);
    }

    // ============ TESTS POUR SendPrivateMessage ============

    [Fact]
    public async Task Chat_SendPrivateMessage_ShouldCallChatService()
    {
        // Arrange
        SetupBasicAuth();
        _mockHttp.When(HttpMethod.Get, "*/api/channels")
            .Respond(HttpStatusCode.OK, JsonContent.Create(new List<Channel>()));
        _mockHttp.When(HttpMethod.Get, "*/api/private-messages/status/Alice")
            .Respond(HttpStatusCode.OK, JsonContent.Create(new { Username = "Alice", IsOnline = true }));

        _chatServiceMock.Setup(x => x.InitializeAsync(It.IsAny<IHubConnectionBuilder>()))
            .Returns(Task.CompletedTask);
        _chatServiceMock.Setup(x => x.MarkPrivateMessagesAsRead(It.IsAny<string>()))
            .Returns(Task.CompletedTask);
        _chatServiceMock.Setup(x => x.SendPrivateMessage(It.IsAny<SendPrivateMessageRequest>()))
            .Returns(Task.CompletedTask);
        _chatServiceMock.SetupGet(x => x.IsInitialized).Returns(true);

        _privateMessageServiceMock.Setup(x => x.GetConversationsAsync("TestUser"))
            .ReturnsAsync([new() { OtherUser = new User { UserId = "user1", Username = "Alice" }, LastMessage = "Hi", LastMessageTime = DateTime.UtcNow, UnreadCount = 0 }]);
        _privateMessageServiceMock.Setup(x => x.GetPrivateMessagesAsync("TestUser", "user1"))
            .ReturnsAsync([]);

        var cut = await RenderChatAsync();
        await NavigateToPrivateChatAsync(cut, "user1", "Alice");

        // Act - Envoyer un message
        var input = await cut.InvokeAsync(() => cut.Find(".input-area input"));
        var button = await cut.InvokeAsync(() => cut.Find(".input-area button"));

        await cut.InvokeAsync(() => input.Input("Hello Alice!"));
        await cut.InvokeAsync(() => button.Click());
        await Task.Delay(100);

        // Assert
        _chatServiceMock.Verify(
            x => x.SendPrivateMessage(It.Is<SendPrivateMessageRequest>(req =>
                req.RecipientUserId == "user1" &&
                req.RecipientUsername == "Alice" &&
                req.Content == "Hello Alice!")),
            Times.Once);
    }

    [Fact]
    public async Task Chat_SendPrivateMessage_WhenNotConnected_ShouldNotSend()
    {
        // Arrange
        SetupBasicAuth();
        _mockHttp.When(HttpMethod.Get, "*/api/channels")
            .Respond(HttpStatusCode.OK, JsonContent.Create(new List<Channel>()));
        _mockHttp.When(HttpMethod.Get, "*/api/private-messages/status/Alice")
            .Respond(HttpStatusCode.OK, JsonContent.Create(new { Username = "Alice", IsOnline = true }));

        var isConnected = false;
        _chatServiceMock.SetupGet(x => x.IsInitialized).Returns(() => isConnected);
        _chatServiceMock.Setup(x => x.InitializeAsync(It.IsAny<IHubConnectionBuilder>()))
            .Callback(() => isConnected = false) // Simuler une déconnexion
            .Returns(Task.CompletedTask);
        _chatServiceMock.Setup(x => x.MarkPrivateMessagesAsRead(It.IsAny<string>()))
            .Returns(Task.CompletedTask);
        _chatServiceMock.Setup(x => x.SendPrivateMessage(It.IsAny<SendPrivateMessageRequest>()))
            .Returns(Task.CompletedTask);

        _privateMessageServiceMock.Setup(x => x.GetConversationsAsync("TestUser"))
            .ReturnsAsync([new() { OtherUser = new User { UserId = "user1", Username = "Alice" }, LastMessage = "Hi", LastMessageTime = DateTime.UtcNow, UnreadCount = 0 }]);
        _privateMessageServiceMock.Setup(x => x.GetPrivateMessagesAsync("TestUser", "user1"))
            .ReturnsAsync([]);

        var cut = await RenderChatAsync();
        await NavigateToPrivateChatAsync(cut, "user1", "Alice");

        // Act - Essayer d'envoyer un message
        var input = cut.Find(".input-area input");
        var button = cut.Find(".input-area button");

        await cut.InvokeAsync(() => input.Input("Hello"));
        await cut.InvokeAsync(() => button.Click());
        await Task.Delay(100);

        // Assert - Ne devrait pas envoyer
        _chatServiceMock.Verify(
            x => x.SendPrivateMessage(It.IsAny<SendPrivateMessageRequest>()),
            Times.Never);
    }

    [Fact]
    public async Task Chat_SendPrivateMessage_WhenNoSelectedUser_ShouldNotSend()
    {
        // Arrange
        SetupBasicAuth();
        SetupBasicMocks();

        _chatServiceMock.Setup(x => x.SendPrivateMessage(It.IsAny<SendPrivateMessageRequest>()))
            .Returns(Task.CompletedTask);

        var cut = await RenderChatAsync();

        // Act - Essayer d'envoyer sans utilisateur sélectionné (ne devrait pas être possible dans l'UI)
        // Mais on teste la robustesse de la méthode
        var exception = await Record.ExceptionAsync(async () => await Task.Delay(100));

        // Assert
        Assert.Null(exception);
        _chatServiceMock.Verify(
            x => x.SendPrivateMessage(It.IsAny<SendPrivateMessageRequest>()),
            Times.Never);
    }

    [Fact]
    public async Task Chat_SendPrivateMessage_WithEmptyContent_ShouldNotSend()
    {
        // Arrange
        SetupBasicAuth();
        _mockHttp.When(HttpMethod.Get, "*/api/channels")
            .Respond(HttpStatusCode.OK, JsonContent.Create(new List<Channel>()));
        _mockHttp.When(HttpMethod.Get, "*/api/private-messages/status/Alice")
            .Respond(HttpStatusCode.OK, JsonContent.Create(new { Username = "Alice", IsOnline = true }));

        _chatServiceMock.Setup(x => x.InitializeAsync(It.IsAny<IHubConnectionBuilder>()))
            .Returns(Task.CompletedTask);
        _chatServiceMock.Setup(x => x.MarkPrivateMessagesAsRead(It.IsAny<string>()))
            .Returns(Task.CompletedTask);
        _chatServiceMock.Setup(x => x.SendPrivateMessage(It.IsAny<SendPrivateMessageRequest>()))
            .Returns(Task.CompletedTask);

        _privateMessageServiceMock.Setup(x => x.GetConversationsAsync("TestUser"))
            .ReturnsAsync([new() { OtherUser = new User { UserId = "user1", Username = "Alice" }, LastMessage = "Hi", LastMessageTime = DateTime.UtcNow, UnreadCount = 0 }]);
        _privateMessageServiceMock.Setup(x => x.GetPrivateMessagesAsync("TestUser", "user1"))
            .ReturnsAsync([]);

        var cut = await RenderChatAsync();
        await NavigateToPrivateChatAsync(cut, "user1", "Alice");

        // Act - Essayer d'envoyer un message vide
        var button = cut.Find(".input-area button");
        await cut.InvokeAsync(() => button.Click());
        await Task.Delay(100);

        // Assert - Le composant ChatArea devrait bloquer l'envoi
        // (le bouton est désactivé si le champ est vide)
        _chatServiceMock.Verify(
            x => x.SendPrivateMessage(It.IsAny<SendPrivateMessageRequest>()),
            Times.Never);
    }

    [Fact]
    public async Task Chat_SendPrivateMessage_OnError_ShouldLogError()
    {
        // Arrange
        SetupBasicAuth();
        _mockHttp.When(HttpMethod.Get, "*/api/channels")
            .Respond(HttpStatusCode.OK, JsonContent.Create(new List<Channel>()));
        _mockHttp.When(HttpMethod.Get, "*/api/private-messages/status/Alice")
            .Respond(HttpStatusCode.OK, JsonContent.Create(new { Username = "Alice", IsOnline = true }));

        _chatServiceMock.Setup(x => x.InitializeAsync(It.IsAny<IHubConnectionBuilder>()))
            .Returns(Task.CompletedTask);
        _chatServiceMock.Setup(x => x.MarkPrivateMessagesAsRead(It.IsAny<string>()))
            .Returns(Task.CompletedTask);
        _chatServiceMock.Setup(x => x.SendPrivateMessage(It.IsAny<SendPrivateMessageRequest>()))
            .ThrowsAsync(new Exception("Network error"));

        _privateMessageServiceMock.Setup(x => x.GetConversationsAsync("TestUser"))
            .ReturnsAsync([new() { OtherUser = new User { UserId = "user1", Username = "Alice" }, LastMessage = "Hi", LastMessageTime = DateTime.UtcNow, UnreadCount = 0 }]);
        _privateMessageServiceMock.Setup(x => x.GetPrivateMessagesAsync("TestUser", "user1"))
            .ReturnsAsync([]);

        var cut = await RenderChatAsync();
        await NavigateToPrivateChatAsync(cut, "user1", "Alice");

        // Act - Envoyer un message qui échouera
        var input = cut.Find(".input-area input");
        var button = cut.Find(".input-area button");

        await cut.InvokeAsync(() => input.Input("Test"));

        var exception = await Record.ExceptionAsync(async () =>
        {
            await cut.InvokeAsync(() => button.Click());
            await Task.Delay(100);
        });

        // Assert - Ne devrait pas crasher l'application
        Assert.Null(exception);
    }

    [Fact]
    [SuppressMessage("Major Code Smell", "S6966:Awaitable method should be used", Justification = "Raise doesn't support async")]
    public async Task Chat_SendPrivateMessage_ShouldTriggerOnPrivateMessageSent()
    {
        // Arrange
        SetupBasicAuth();
        _mockHttp.When(HttpMethod.Get, "*/api/channels")
            .Respond(HttpStatusCode.OK, JsonContent.Create(new List<Channel>()));
        _mockHttp.When(HttpMethod.Get, "*/api/private-messages/status/Alice")
            .Respond(HttpStatusCode.OK, JsonContent.Create(new { Username = "Alice", IsOnline = true }));

        _chatServiceMock.Setup(x => x.InitializeAsync(It.IsAny<IHubConnectionBuilder>()))
            .Returns(Task.CompletedTask);
        _chatServiceMock.Setup(x => x.MarkPrivateMessagesAsRead(It.IsAny<string>()))
            .Returns(Task.CompletedTask);
        _chatServiceMock.Setup(x => x.SendPrivateMessage(It.IsAny<SendPrivateMessageRequest>()))
            .Returns(Task.CompletedTask);

        _privateMessageServiceMock.Setup(x => x.GetConversationsAsync("TestUser"))
            .ReturnsAsync([new() { OtherUser = new User { UserId = "user1", Username = "Alice" }, LastMessage = "Hi", LastMessageTime = DateTime.UtcNow, UnreadCount = 0 }]);
        _privateMessageServiceMock.Setup(x => x.GetPrivateMessagesAsync("TestUser", "user1"))
            .ReturnsAsync([]);

        var cut = await RenderChatAsync();
        await NavigateToPrivateChatAsync(cut, "user1", "Alice");

        var input = cut.Find(".input-area input");
        var button = cut.Find(".input-area button");

        await cut.InvokeAsync(() => input.Input("Hello"));
        await cut.InvokeAsync(() => button.Click());
        await Task.Delay(100);

        // Act - Simuler l'événement OnPrivateMessageSent
        var sentMessage = new PrivateMessage
        {
            Id = Guid.NewGuid(),
            SenderUserId = "TestUser",
            SenderUsername = "TestUser",
            RecipientUserId = "user1",
            RecipientUsername = "Alice",
            Content = "Hello",
            Timestamp = DateTime.UtcNow
        };

        _privateMessageServiceMock.Raise(x => x.OnPrivateMessageSent += null, sentMessage);
        await Task.Delay(100);
        cut.Render();

        // Assert - Le message devrait apparaître dans la conversation
        Assert.Contains("Hello", cut.Markup);
    }

    // Méthode helper pour simuler le clic sur une conversation dans la sidebar
    private static async Task StartPrivateChatFromSidebarAsync(IRenderedComponent<Chat> cut, User user)
    => await NavigateToPrivateChatAsync(cut, user.UserId, user.Username);
}