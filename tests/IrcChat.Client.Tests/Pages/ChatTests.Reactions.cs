// tests/IrcChat.Client.Tests/Pages/ChatTests.Reactions.cs
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Http.Json;
using IrcChat.Client.Pages;
using IrcChat.Shared.Models;
using Microsoft.AspNetCore.SignalR.Client;
using RichardSzalay.MockHttp;

namespace IrcChat.Client.Tests.Pages;

public partial class ChatTests
{
    // ===== Souscription à l'événement OnMessageReactionUpdated =====

    [Fact]
    public async Task Chat_OnInitialization_ShouldSubscribeToMessageReactionUpdatedEvent()
    {
        // Arrange
        SetupBasicAuth();
        SetupBasicMocks();

        // Act
        Render<Chat>();
        await Task.Delay(200);

        // Assert
        chatServiceMock.VerifyAdd(x => x.OnMessageReactionUpdated += It.IsAny<Action<Guid, List<MessageReactionDto>>>());
    }

    [Fact]
    public async Task Chat_DisposeAsync_ShouldUnsubscribeFromMessageReactionUpdatedEvent()
    {
        // Arrange
        SetupBasicAuth();
        SetupBasicMocks();
        var cut = Render<Chat>();
        await Task.Delay(200);

        // Act
        await cut.Instance.DisposeAsync();

        // Assert
        chatServiceMock.VerifyRemove(x => x.OnMessageReactionUpdated -= It.IsAny<Action<Guid, List<MessageReactionDto>>>());
    }

    // ===== Mise à jour des réactions en temps réel =====

    [Fact]
    [SuppressMessage("Major Code Smell", "S6966:Awaitable method should be used", Justification = "Raise doesn't support async")]
    public async Task Chat_OnMessageReactionUpdated_WhenMessageExists_ShouldUpdateReactions()
    {
        // Arrange
        SetupBasicAuth();
        var messageId = Guid.NewGuid();
        var channels = new List<Channel>
        {
            new() { Id = Guid.NewGuid(), Name = "general", CreatedBy = "system", CreatedAt = DateTime.UtcNow },
        };
        var messages = new List<Message>
        {
            new() { Id = messageId, Username = "User1", Content = "Hello", Channel = "general", Timestamp = DateTime.UtcNow, Reactions = [] },
        };

        mockHttp.When(HttpMethod.Get, "*/api/channels")
            .Respond(HttpStatusCode.OK, request => JsonContent.Create(channels));
        mockHttp.When(HttpMethod.Get, "*/api/my-channels?username=TestUser")
            .Respond(HttpStatusCode.OK, request => JsonContent.Create(channels));
        mockHttp.When(HttpMethod.Get, "*/api/messages/general")
            .Respond(HttpStatusCode.OK, request => JsonContent.Create(messages));
        mockHttp.When(HttpMethod.Get, "*/api/channels/general/users")
            .Respond(HttpStatusCode.OK, request => JsonContent.Create(new List<User>()));

        chatServiceMock.Setup(x => x.InitializeAsync(It.IsAny<IHubConnectionBuilder>()))
            .Returns(Task.CompletedTask);
        chatServiceMock.Setup(x => x.JoinChannel("general")).Returns(Task.CompletedTask);
        privateMessageServiceMock.Setup(x => x.GetConversationsAsync(It.IsAny<string>()))
            .ReturnsAsync([]);

        var cut = await RenderChatAsync(channelName: "general");

        var updatedReactions = new List<MessageReactionDto>
        {
            new() { Emoji = "👍", Count = 1, UserIds = ["u1"], Usernames = ["alice"] },
        };

        // Act — simuler la mise à jour des réactions via SignalR
        chatServiceMock.Raise(x => x.OnMessageReactionUpdated += null, messageId, updatedReactions);
        await Task.Delay(100);
        cut.Render();

        // Assert — le badge de réaction est affiché
        Assert.Contains("reaction-badge", cut.Markup);
        Assert.Contains("👍", cut.Markup);
    }

    [Fact]
    public async Task Chat_OnMessageReactionUpdated_WhenMessageNotInCurrentView_ShouldIgnore()
    {
        // Arrange
        SetupBasicAuth();
        SetupBasicMocks();
        Render<Chat>();
        await Task.Delay(200);

        var unknownMessageId = Guid.NewGuid();
        var reactions = new List<MessageReactionDto>
        {
            new() { Emoji = "👍", Count = 1, UserIds = ["u1"], Usernames = ["alice"] },
        };

        // Act — réaction pour un message inconnu (ne pas crasher)
        var exception = Record.Exception(() =>
            chatServiceMock.Raise(x => x.OnMessageReactionUpdated += null, unknownMessageId, reactions));

        await Task.Delay(100);

        // Assert — pas d'exception
        Assert.Null(exception);
    }

    [Fact]
    [SuppressMessage("Major Code Smell", "S6966:Awaitable method should be used", Justification = "Raise doesn't support async")]
    public async Task Chat_OnMessageReactionUpdated_WhenReactionsEmpty_ShouldClearReactions()
    {
        // Arrange
        SetupBasicAuth();
        var messageId = Guid.NewGuid();
        var channels = new List<Channel>
        {
            new() { Id = Guid.NewGuid(), Name = "general", CreatedBy = "system", CreatedAt = DateTime.UtcNow },
        };
        var messages = new List<Message>
        {
            new()
            {
                Id = messageId,
                Username = "User1",
                Content = "Hello",
                Channel = "general",
                Timestamp = DateTime.UtcNow,
                Reactions =
                [
                    new MessageReactionDto { Emoji = "👍", Count = 1, UserIds = ["u1"], Usernames = ["alice"] },
                ],
            },
        };

        mockHttp.When(HttpMethod.Get, "*/api/channels")
            .Respond(HttpStatusCode.OK, request => JsonContent.Create(channels));
        mockHttp.When(HttpMethod.Get, "*/api/my-channels?username=TestUser")
            .Respond(HttpStatusCode.OK, request => JsonContent.Create(channels));
        mockHttp.When(HttpMethod.Get, "*/api/messages/general")
            .Respond(HttpStatusCode.OK, request => JsonContent.Create(messages));
        mockHttp.When(HttpMethod.Get, "*/api/channels/general/users")
            .Respond(HttpStatusCode.OK, request => JsonContent.Create(new List<User>()));

        chatServiceMock.Setup(x => x.InitializeAsync(It.IsAny<IHubConnectionBuilder>()))
            .Returns(Task.CompletedTask);
        chatServiceMock.Setup(x => x.JoinChannel("general")).Returns(Task.CompletedTask);
        privateMessageServiceMock.Setup(x => x.GetConversationsAsync(It.IsAny<string>()))
            .ReturnsAsync([]);

        var cut = await RenderChatAsync(channelName: "general");

        // Act — liste vide = toutes les réactions retirées
        chatServiceMock.Raise(x => x.OnMessageReactionUpdated += null, messageId, new List<MessageReactionDto>());
        await Task.Delay(100);
        cut.Render();

        // Assert — plus de badge
        Assert.Throws<ElementNotFoundException>(() => cut.Find(".reaction-badge"));
    }

    // ===== HandleReact — appel à ChatService =====

    [Fact]
    public async Task Chat_HandleReact_ShouldCallChatServiceReactToMessage()
    {
        // Arrange
        SetupBasicAuth();
        var messageId = Guid.NewGuid();
        var channels = new List<Channel>
        {
            new() { Id = Guid.NewGuid(), Name = "general", CreatedBy = "system", CreatedAt = DateTime.UtcNow },
        };
        var messages = new List<Message>
        {
            new() { Id = messageId, Username = "User1", Content = "Hello", Channel = "general", Timestamp = DateTime.UtcNow },
        };

        mockHttp.When(HttpMethod.Get, "*/api/channels")
            .Respond(HttpStatusCode.OK, request => JsonContent.Create(channels));
        mockHttp.When(HttpMethod.Get, "*/api/my-channels?username=TestUser")
            .Respond(HttpStatusCode.OK, request => JsonContent.Create(channels));
        mockHttp.When(HttpMethod.Get, "*/api/messages/general")
            .Respond(HttpStatusCode.OK, request => JsonContent.Create(messages));
        mockHttp.When(HttpMethod.Get, "*/api/channels/general/users")
            .Respond(HttpStatusCode.OK, request => JsonContent.Create(new List<User>()));

        chatServiceMock.Setup(x => x.InitializeAsync(It.IsAny<IHubConnectionBuilder>()))
            .Returns(Task.CompletedTask);
        chatServiceMock.Setup(x => x.JoinChannel("general")).Returns(Task.CompletedTask);
        chatServiceMock.Setup(x => x.ReactToMessage(It.IsAny<Guid>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);
        privateMessageServiceMock.Setup(x => x.GetConversationsAsync(It.IsAny<string>()))
            .ReturnsAsync([]);

        var cut = await RenderChatAsync(channelName: "general");

        // Act — ouvrir le picker et cliquer sur un emoji
        var addBtn = cut.Find(".add-reaction-btn");
        await cut.InvokeAsync(() => addBtn.Click());
        var quickBtn = cut.Find(".quick-emoji-btn");
        await cut.InvokeAsync(() => quickBtn.Click());
        await Task.Delay(100);

        // Assert
        chatServiceMock.Verify(
            x => x.ReactToMessage(messageId, "👍"),
            Times.Once);
    }

    [Fact]
    public async Task Chat_HandleReact_WhenChatServiceThrows_ShouldHandleGracefully()
    {
        // Arrange
        SetupBasicAuth();
        var messageId = Guid.NewGuid();
        var channels = new List<Channel>
        {
            new() { Id = Guid.NewGuid(), Name = "general", CreatedBy = "system", CreatedAt = DateTime.UtcNow },
        };
        var messages = new List<Message>
        {
            new() { Id = messageId, Username = "User1", Content = "Hello", Channel = "general", Timestamp = DateTime.UtcNow },
        };

        mockHttp.When(HttpMethod.Get, "*/api/channels")
            .Respond(HttpStatusCode.OK, request => JsonContent.Create(channels));
        mockHttp.When(HttpMethod.Get, "*/api/my-channels?username=TestUser")
            .Respond(HttpStatusCode.OK, request => JsonContent.Create(channels));
        mockHttp.When(HttpMethod.Get, "*/api/messages/general")
            .Respond(HttpStatusCode.OK, request => JsonContent.Create(messages));
        mockHttp.When(HttpMethod.Get, "*/api/channels/general/users")
            .Respond(HttpStatusCode.OK, request => JsonContent.Create(new List<User>()));

        chatServiceMock.Setup(x => x.InitializeAsync(It.IsAny<IHubConnectionBuilder>()))
            .Returns(Task.CompletedTask);
        chatServiceMock.Setup(x => x.JoinChannel("general")).Returns(Task.CompletedTask);
        chatServiceMock.Setup(x => x.ReactToMessage(It.IsAny<Guid>(), It.IsAny<string>()))
            .ThrowsAsync(new InvalidOperationException("Hub non initialisé"));
        privateMessageServiceMock.Setup(x => x.GetConversationsAsync(It.IsAny<string>()))
            .ReturnsAsync([]);

        var cut = await RenderChatAsync(channelName: "general");

        // Act & Assert — ne doit pas crasher même si le service throw
        var exception = await Record.ExceptionAsync(async () =>
        {
            var addBtn = cut.Find(".add-reaction-btn");
            await cut.InvokeAsync(() => addBtn.Click());
            var quickBtn = cut.Find(".quick-emoji-btn");
            await cut.InvokeAsync(() => quickBtn.Click());
            await Task.Delay(100);
        });

        Assert.Null(exception);
    }

    // ===== ShowReactions uniquement pour les salons publics =====

    [Fact]
    public async Task Chat_WhenInPublicChannel_ShouldShowReactionsOnMessages()
    {
        // Arrange
        SetupBasicAuth();
        var channels = new List<Channel>
        {
            new() { Id = Guid.NewGuid(), Name = "general", CreatedBy = "system", CreatedAt = DateTime.UtcNow },
        };
        var messages = new List<Message>
        {
            new() { Id = Guid.NewGuid(), Username = "User1", Content = "Hello", Channel = "general", Timestamp = DateTime.UtcNow },
        };

        mockHttp.When(HttpMethod.Get, "*/api/channels")
            .Respond(HttpStatusCode.OK, request => JsonContent.Create(channels));
        mockHttp.When(HttpMethod.Get, "*/api/my-channels?username=TestUser")
            .Respond(HttpStatusCode.OK, request => JsonContent.Create(channels));
        mockHttp.When(HttpMethod.Get, "*/api/messages/general")
            .Respond(HttpStatusCode.OK, request => JsonContent.Create(messages));
        mockHttp.When(HttpMethod.Get, "*/api/channels/general/users")
            .Respond(HttpStatusCode.OK, request => JsonContent.Create(new List<User>()));

        chatServiceMock.Setup(x => x.InitializeAsync(It.IsAny<IHubConnectionBuilder>()))
            .Returns(Task.CompletedTask);
        chatServiceMock.Setup(x => x.JoinChannel("general")).Returns(Task.CompletedTask);
        privateMessageServiceMock.Setup(x => x.GetConversationsAsync(It.IsAny<string>()))
            .ReturnsAsync([]);

        // Act
        var cut = await RenderChatAsync(channelName: "general");
        cut.Render();

        // Assert — le bouton de réaction est affiché
        Assert.Contains("add-reaction-btn", cut.Markup);
    }

    [Fact]
    public async Task Chat_WhenInPrivateConversation_ShouldNotShowReactions()
    {
        // Arrange
        SetupBasicAuth();
        mockHttp.When(HttpMethod.Get, "*/api/channels")
            .Respond(HttpStatusCode.OK, request => JsonContent.Create(new List<Channel>()));
        mockHttp.When(HttpMethod.Get, "*/api/private-messages/status/Friend")
            .Respond(HttpStatusCode.OK, request => JsonContent.Create(new { Username = "Friend", IsOnline = true }));

        chatServiceMock.Setup(x => x.InitializeAsync(It.IsAny<IHubConnectionBuilder>()))
            .Returns(Task.CompletedTask);
        chatServiceMock.Setup(x => x.MarkPrivateMessagesAsRead(It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        var privateMessages = new List<PrivateMessage>
        {
            new() { Id = Guid.NewGuid(), SenderUserId = "Friend", SenderUsername = "Friend", RecipientUserId = "TestUser", RecipientUsername = "TestUser", Content = "Hi!", Timestamp = DateTime.UtcNow },
        };

        privateMessageServiceMock.Setup(x => x.GetConversationsAsync("TestUser"))
            .ReturnsAsync([new PrivateConversation { OtherUser = new User { UserId = "Friend", Username = "Friend" }, LastMessage = "Hi", LastMessageTime = DateTime.UtcNow, UnreadCount = 0 }]);
        privateMessageServiceMock.Setup(x => x.GetPrivateMessagesAsync("TestUser", "Friend"))
            .ReturnsAsync(privateMessages);

        var cut = await RenderChatAsync();
        await NavigateToPrivateChatAsync(cut, "Friend", "Friend");
        cut.Render();

        // Assert — pas de bouton de réaction en conversation privée
        Assert.Throws<ElementNotFoundException>(() => cut.Find(".add-reaction-btn"));
    }

    // ===== CurrentUserId passé au composant =====

    [Fact]
    public async Task Chat_ShouldPassCurrentUserIdToMessageList()
    {
        // Arrange
        SetupBasicAuth();
        var channels = new List<Channel>
        {
            new() { Id = Guid.NewGuid(), Name = "general", CreatedBy = "system", CreatedAt = DateTime.UtcNow },
        };
        var currentUserId = "TestUser";
        var messages = new List<Message>
        {
            new()
            {
                Id = Guid.NewGuid(),
                Username = "User1",
                Content = "Hello",
                Channel = "general",
                Timestamp = DateTime.UtcNow,
                Reactions =
                [
                    new MessageReactionDto { Emoji = "👍", Count = 1, UserIds = [currentUserId], Usernames = ["TestUser"] },
                ],
            },
        };

        mockHttp.When(HttpMethod.Get, "*/api/channels")
            .Respond(HttpStatusCode.OK, request => JsonContent.Create(channels));
        mockHttp.When(HttpMethod.Get, "*/api/my-channels?username=TestUser")
            .Respond(HttpStatusCode.OK, request => JsonContent.Create(channels));
        mockHttp.When(HttpMethod.Get, "*/api/messages/general")
            .Respond(HttpStatusCode.OK, request => JsonContent.Create(messages));
        mockHttp.When(HttpMethod.Get, "*/api/channels/general/users")
            .Respond(HttpStatusCode.OK, request => JsonContent.Create(new List<User>()));

        chatServiceMock.Setup(x => x.InitializeAsync(It.IsAny<IHubConnectionBuilder>()))
            .Returns(Task.CompletedTask);
        chatServiceMock.Setup(x => x.JoinChannel("general")).Returns(Task.CompletedTask);
        privateMessageServiceMock.Setup(x => x.GetConversationsAsync(It.IsAny<string>()))
            .ReturnsAsync([]);

        // Act
        var cut = await RenderChatAsync(channelName: "general");
        cut.Render();

        // Assert — la réaction doit avoir la classe "own" (CurrentUserId est correctement passé)
        Assert.Contains("reaction-badge own", cut.Markup);
    }
}