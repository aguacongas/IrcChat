// tests/IrcChat.Client.Tests/Pages/ChatTests.MessageDeletion.cs
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Http.Json;
using IrcChat.Shared.Models;
using Microsoft.AspNetCore.SignalR.Client;
using RichardSzalay.MockHttp;

namespace IrcChat.Client.Tests.Pages;

/// <summary>
/// Tests unitaires pour la suppression de messages dans Chat.razor
/// Classe partielle qui étend ChatTests.cs.
/// </summary>
public partial class ChatTests
{
    // ============ TESTS POUR OnMessageDeleted ============

    [Fact]
    [SuppressMessage("Major Code Smell", "S6966:Awaitable method should be used", Justification = "Raise doesn't support async")]
    public async Task Chat_OnMessageDeleted_InCurrentChannel_ShouldRemoveMessage()
    {
        // Arrange
        SetupBasicAuth();
        var channels = new List<Channel>
        {
            new() { Id = Guid.NewGuid(), Name = "general", CreatedBy = "system", CreatedAt = DateTime.UtcNow },
        };
        var messageId = Guid.NewGuid();
        var messages = new List<Message>
        {
            new() { Id = messageId, Username = "User1", UserId = "User1", Content = "Message to delete", Channel = "general", Timestamp = DateTime.UtcNow },
            new() { Id = Guid.NewGuid(), Username = "User2", UserId = "User2", Content = "Another message", Channel = "general", Timestamp = DateTime.UtcNow },
        };

        mockHttp.When(HttpMethod.Get, "*/api/channels")
            .Respond(HttpStatusCode.OK, request => JsonContent.Create(channels));
        mockHttp.When(HttpMethod.Get, "*/api/my-channels?username=TestUser")
            .Respond(HttpStatusCode.OK, request => JsonContent.Create(channels));
        mockHttp.When(HttpMethod.Get, "*/api/messages/general*")
            .Respond(HttpStatusCode.OK, request => JsonContent.Create(messages));
        mockHttp.When(HttpMethod.Get, "*/api/channels/general/users")
            .Respond(HttpStatusCode.OK, request => JsonContent.Create(new List<User>()));

        chatServiceMock.Setup(x => x.InitializeAsync(It.IsAny<IHubConnectionBuilder>()))
            .Returns(Task.CompletedTask);
        chatServiceMock.Setup(x => x.JoinChannel("general")).Returns(Task.CompletedTask);
        privateMessageServiceMock.Setup(x => x.GetConversationsAsync(It.IsAny<string>()))
            .ReturnsAsync([]);

        var cut = await RenderChatAsync(channelName: "general");
        await Task.Delay(200);
        cut.Render();

        // Vérifier que le message est présent
        Assert.Contains("Message to delete", cut.Markup);
        Assert.Contains("Another message", cut.Markup);

        // Act - Lever l'événement OnMessageDeleted
        chatServiceMock.Raise(x => x.OnMessageDeleted += null, messageId, "general");
        await Task.Delay(200);
        cut.Render();

        // Assert - Le message devrait être supprimé
        Assert.DoesNotContain("Message to delete", cut.Markup);
        Assert.Contains("Another message", cut.Markup);
    }

    [Fact]
    [SuppressMessage("Major Code Smell", "S6966:Awaitable method should be used", Justification = "Raise doesn't support async")]
    public async Task Chat_OnMessageDeleted_WhenDifferentChannel_ShouldNotRemove()
    {
        // Arrange
        SetupBasicAuth();
        var channels = new List<Channel>
        {
            new() { Id = Guid.NewGuid(), Name = "general", CreatedBy = "system", CreatedAt = DateTime.UtcNow },
            new() { Id = Guid.NewGuid(), Name = "random", CreatedBy = "system", CreatedAt = DateTime.UtcNow },
        };
        var messageId = Guid.NewGuid();
        var messages = new List<Message>
        {
            new() { Id = messageId, Username = "User1", UserId = "User1", Content = "Message in general", Channel = "general", Timestamp = DateTime.UtcNow },
        };

        mockHttp.When(HttpMethod.Get, "*/api/channels")
            .Respond(HttpStatusCode.OK, request => JsonContent.Create(channels));
        mockHttp.When(HttpMethod.Get, "*/api/my-channels?username=TestUser")
            .Respond(HttpStatusCode.OK, request => JsonContent.Create(channels));
        mockHttp.When(HttpMethod.Get, "*/api/messages/general*")
            .Respond(HttpStatusCode.OK, request => JsonContent.Create(messages));
        mockHttp.When(HttpMethod.Get, "*/api/channels/general/users")
            .Respond(HttpStatusCode.OK, request => JsonContent.Create(new List<User>()));

        chatServiceMock.Setup(x => x.InitializeAsync(It.IsAny<IHubConnectionBuilder>()))
            .Returns(Task.CompletedTask);
        chatServiceMock.Setup(x => x.JoinChannel("general")).Returns(Task.CompletedTask);
        privateMessageServiceMock.Setup(x => x.GetConversationsAsync(It.IsAny<string>()))
            .ReturnsAsync([]);

        var cut = await RenderChatAsync(channelName: "general");
        await Task.Delay(200);
        cut.Render();

        Assert.Contains("Message in general", cut.Markup);

        // Act - Supprimer un message d'un autre canal
        chatServiceMock.Raise(x => x.OnMessageDeleted += null, messageId, "random");
        await Task.Delay(200);
        cut.Render();

        // Assert - Le message devrait toujours être présent
        Assert.Contains("Message in general", cut.Markup);
    }

    [Fact]
    [SuppressMessage("Major Code Smell", "S6966:Awaitable method should be used", Justification = "Raise doesn't support async")]
    public async Task Chat_OnMessageDeleted_WhenInPrivateChat_ShouldIgnore()
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
        privateMessageServiceMock.Setup(x => x.GetConversationsAsync("TestUser"))
            .ReturnsAsync([new() { OtherUser = new User { UserId = "Friend", Username = "Friend" }, LastMessage = "Hi", LastMessageTime = DateTime.UtcNow, UnreadCount = 0 }]);
        privateMessageServiceMock.Setup(x => x.GetPrivateMessagesAsync("TestUser", "Friend"))
            .ReturnsAsync([]);

        var cut = await RenderChatAsync();
        await NavigateToPrivateChatAsync(cut, "Friend", "Friend");
        await Task.Delay(200);

        // Act - Lever l'événement OnMessageDeleted
        var exception = await Record.ExceptionAsync(async () =>
        {
            chatServiceMock.Raise(x => x.OnMessageDeleted += null, Guid.NewGuid(), "general");
            await Task.Delay(200);
        });

        // Assert - Ne devrait pas crasher
        Assert.Null(exception);
    }

    [Fact]
    [SuppressMessage("Major Code Smell", "S6966:Awaitable method should be used", Justification = "Raise doesn't support async")]
    public async Task Chat_OnMessageDeleted_WhenMessageNotInList_ShouldNotThrow()
    {
        // Arrange
        SetupBasicAuth();
        var channels = new List<Channel>
        {
            new() { Id = Guid.NewGuid(), Name = "general", CreatedBy = "system", CreatedAt = DateTime.UtcNow },
        };
        var messages = new List<Message>
        {
            new() { Id = Guid.NewGuid(), Username = "User1", UserId = "User1", Content = "Existing message", Channel = "general", Timestamp = DateTime.UtcNow },
        };

        mockHttp.When(HttpMethod.Get, "*/api/channels")
            .Respond(HttpStatusCode.OK, request => JsonContent.Create(channels));
        mockHttp.When(HttpMethod.Get, "*/api/my-channels?username=TestUser")
            .Respond(HttpStatusCode.OK, request => JsonContent.Create(channels));
        mockHttp.When(HttpMethod.Get, "*/api/messages/general*")
            .Respond(HttpStatusCode.OK, request => JsonContent.Create(messages));
        mockHttp.When(HttpMethod.Get, "*/api/channels/general/users")
            .Respond(HttpStatusCode.OK, request => JsonContent.Create(new List<User>()));

        chatServiceMock.Setup(x => x.InitializeAsync(It.IsAny<IHubConnectionBuilder>()))
            .Returns(Task.CompletedTask);
        chatServiceMock.Setup(x => x.JoinChannel("general")).Returns(Task.CompletedTask);
        privateMessageServiceMock.Setup(x => x.GetConversationsAsync(It.IsAny<string>()))
            .ReturnsAsync([]);

        var cut = await RenderChatAsync(channelName: "general");
        await Task.Delay(200);

        // Act - Supprimer un message qui n'existe pas
        var exception = await Record.ExceptionAsync(async () =>
        {
            chatServiceMock.Raise(x => x.OnMessageDeleted += null, Guid.NewGuid(), "general");
            await Task.Delay(200);
        });

        // Assert - Ne devrait pas lever d'exception
        Assert.Null(exception);
    }

    [Fact]
    [SuppressMessage("Major Code Smell", "S6966:Awaitable method should be used", Justification = "Raise doesn't support async")]
    public async Task Chat_OnMessageDeleted_MultipleTimes_ShouldRemoveAllMessages()
    {
        // Arrange
        SetupBasicAuth();
        var channels = new List<Channel>
        {
            new() { Id = Guid.NewGuid(), Name = "general", CreatedBy = "system", CreatedAt = DateTime.UtcNow },
        };
        var messageId1 = Guid.NewGuid();
        var messageId2 = Guid.NewGuid();
        var messageId3 = Guid.NewGuid();
        var messages = new List<Message>
        {
            new() { Id = messageId1, Username = "User1", UserId = "User1", Content = "Message 1", Channel = "general", Timestamp = DateTime.UtcNow },
            new() { Id = messageId2, Username = "User2", UserId = "User2", Content = "Message 2", Channel = "general", Timestamp = DateTime.UtcNow },
            new() { Id = messageId3, Username = "User3", UserId = "User3", Content = "Message 3", Channel = "general", Timestamp = DateTime.UtcNow },
        };

        mockHttp.When(HttpMethod.Get, "*/api/channels")
            .Respond(HttpStatusCode.OK, request => JsonContent.Create(channels));
        mockHttp.When(HttpMethod.Get, "*/api/my-channels?username=TestUser")
            .Respond(HttpStatusCode.OK, request => JsonContent.Create(channels));
        mockHttp.When(HttpMethod.Get, "*/api/messages/general*")
            .Respond(HttpStatusCode.OK, request => JsonContent.Create(messages));
        mockHttp.When(HttpMethod.Get, "*/api/channels/general/users")
            .Respond(HttpStatusCode.OK, request => JsonContent.Create(new List<User>()));

        chatServiceMock.Setup(x => x.InitializeAsync(It.IsAny<IHubConnectionBuilder>()))
            .Returns(Task.CompletedTask);
        chatServiceMock.Setup(x => x.JoinChannel("general")).Returns(Task.CompletedTask);
        privateMessageServiceMock.Setup(x => x.GetConversationsAsync(It.IsAny<string>()))
            .ReturnsAsync([]);

        var cut = await RenderChatAsync(channelName: "general");
        await Task.Delay(200);
        cut.Render();

        Assert.Contains("Message 1", cut.Markup);
        Assert.Contains("Message 2", cut.Markup);
        Assert.Contains("Message 3", cut.Markup);

        // Act - Supprimer les messages un par un
        chatServiceMock.Raise(x => x.OnMessageDeleted += null, messageId1, "general");
        await Task.Delay(100);
        cut.Render();

        chatServiceMock.Raise(x => x.OnMessageDeleted += null, messageId3, "general");
        await Task.Delay(100);
        cut.Render();

        // Assert - Seul Message 2 devrait rester
        Assert.DoesNotContain("Message 1", cut.Markup);
        Assert.Contains("Message 2", cut.Markup);
        Assert.DoesNotContain("Message 3", cut.Markup);
    }

    [Fact]
    [SuppressMessage("Major Code Smell", "S6966:Awaitable method should be used", Justification = "Raise doesn't support async")]
    public async Task Chat_OnMessageDeleted_ShouldTriggerStateHasChanged()
    {
        // Arrange
        SetupBasicAuth();
        var channels = new List<Channel>
        {
            new() { Id = Guid.NewGuid(), Name = "general", CreatedBy = "system", CreatedAt = DateTime.UtcNow },
        };
        var messageId = Guid.NewGuid();
        var messages = new List<Message>
        {
            new() { Id = messageId, Username = "User1", UserId = "User1", Content = "Test message", Channel = "general", Timestamp = DateTime.UtcNow },
        };

        mockHttp.When(HttpMethod.Get, "*/api/channels")
            .Respond(HttpStatusCode.OK, request => JsonContent.Create(channels));
        mockHttp.When(HttpMethod.Get, "*/api/my-channels?username=TestUser")
            .Respond(HttpStatusCode.OK, request => JsonContent.Create(channels));
        mockHttp.When(HttpMethod.Get, "*/api/messages/general*")
            .Respond(HttpStatusCode.OK, request => JsonContent.Create(messages));
        mockHttp.When(HttpMethod.Get, "*/api/channels/general/users")
            .Respond(HttpStatusCode.OK, request => JsonContent.Create(new List<User>()));

        chatServiceMock.Setup(x => x.InitializeAsync(It.IsAny<IHubConnectionBuilder>()))
            .Returns(Task.CompletedTask);
        chatServiceMock.Setup(x => x.JoinChannel("general")).Returns(Task.CompletedTask);
        privateMessageServiceMock.Setup(x => x.GetConversationsAsync(It.IsAny<string>()))
            .ReturnsAsync([]);

        var cut = await RenderChatAsync(channelName: "general");
        await Task.Delay(200);
        var renderCountBefore = cut.RenderCount;

        // Act
        chatServiceMock.Raise(x => x.OnMessageDeleted += null, messageId, "general");
        await Task.Delay(200);

        // Assert - Le composant devrait avoir été re-rendu
        Assert.True(cut.RenderCount > renderCountBefore);
    }

    [Fact]
    [SuppressMessage("Major Code Smell", "S6966:Awaitable method should be used", Justification = "Raise doesn't support async")]
    public async Task Chat_OnMessageDeleted_WhenNoCurrentChannel_ShouldDoNothing()
    {
        // Arrange
        SetupBasicAuth();
        mockHttp.When(HttpMethod.Get, "*/api/channels")
            .Respond(HttpStatusCode.OK, request => JsonContent.Create(new List<Channel>()));
        mockHttp.When(HttpMethod.Get, "*/api/my-channels?username=TestUser")
            .Respond(HttpStatusCode.OK, request => JsonContent.Create(new List<Channel>()));

        chatServiceMock.Setup(x => x.InitializeAsync(It.IsAny<IHubConnectionBuilder>()))
            .Returns(Task.CompletedTask);
        privateMessageServiceMock.Setup(x => x.GetConversationsAsync(It.IsAny<string>()))
            .ReturnsAsync([]);

        var cut = await RenderChatAsync(); // Pas de canal actif
        await Task.Delay(200);

        // Act
        var exception = await Record.ExceptionAsync(async () =>
        {
            chatServiceMock.Raise(x => x.OnMessageDeleted += null, Guid.NewGuid(), "general");
            await Task.Delay(200);
        });

        // Assert - Ne devrait pas crasher
        Assert.Null(exception);
    }

    // ============ TESTS POUR HandleDeleteMessage ============

    [Fact]
    public async Task Chat_HandleDeleteMessage_ShouldCallDeleteEndpointWithCorrectUrl()
    {
        // Arrange
        SetupBasicAuth();
        authServiceMock.Setup(x => x.Token).Returns("test-token");

        var channels = new List<Channel>
        {
            new() { Id = Guid.NewGuid(), Name = "general", CreatedBy = "system", CreatedAt = DateTime.UtcNow },
        };
        var messageId = Guid.NewGuid();
        var messages = new List<Message>
        {
            new() { Id = messageId, Username = "User1", UserId = "User1", Content = "Test message", Channel = "general", Timestamp = DateTime.UtcNow },
        };

        mockHttp.When(HttpMethod.Get, "*/api/channels")
            .Respond(HttpStatusCode.OK, request => JsonContent.Create(channels));
        mockHttp.When(HttpMethod.Get, "*/api/my-channels?username=TestUser")
            .Respond(HttpStatusCode.OK, request => JsonContent.Create(channels));
        mockHttp.When(HttpMethod.Get, "*/api/messages/general*")
            .Respond(HttpStatusCode.OK, request => JsonContent.Create(messages));
        mockHttp.When(HttpMethod.Get, "*/api/channels/general/users")
            .Respond(HttpStatusCode.OK, request => JsonContent.Create(new List<User>()));

        var deleteRequest = mockHttp.When(HttpMethod.Delete, $"*/api/messages/general/{messageId}")
            .Respond(HttpStatusCode.NoContent);

        chatServiceMock.Setup(x => x.InitializeAsync(It.IsAny<IHubConnectionBuilder>()))
            .Returns(Task.CompletedTask);
        chatServiceMock.Setup(x => x.JoinChannel("general")).Returns(Task.CompletedTask);
        privateMessageServiceMock.Setup(x => x.GetConversationsAsync(It.IsAny<string>()))
            .ReturnsAsync([]);

        var cut = await RenderChatAsync(channelName: "general");
        await Task.Delay(200);

        // Act - Déclencher HandleDeleteMessage via l'instance
        await cut.InvokeAsync(() => cut.Instance.GetType()
            .GetMethod("HandleDeleteMessage", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .Invoke(cut.Instance, [messageId]));
        await Task.Delay(200);

        // Assert
        Assert.Equal(1, mockHttp.GetMatchCount(deleteRequest));
    }

    [Fact]
    public async Task Chat_HandleDeleteMessage_ShouldIncludeAuthorizationHeader()
    {
        // Arrange
        SetupBasicAuth();
        var testToken = "test-jwt-token-12345";
        authServiceMock.Setup(x => x.Token).Returns(testToken);

        var channels = new List<Channel>
        {
            new() { Id = Guid.NewGuid(), Name = "general", CreatedBy = "system", CreatedAt = DateTime.UtcNow },
        };
        var messageId = Guid.NewGuid();
        var messages = new List<Message>
        {
            new() { Id = messageId, Username = "User1", UserId = "User1", Content = "Test", Channel = "general", Timestamp = DateTime.UtcNow },
        };

        mockHttp.When(HttpMethod.Get, "*/api/channels")
            .Respond(HttpStatusCode.OK, request => JsonContent.Create(channels));
        mockHttp.When(HttpMethod.Get, "*/api/my-channels?username=TestUser")
            .Respond(HttpStatusCode.OK, request => JsonContent.Create(channels));
        mockHttp.When(HttpMethod.Get, "*/api/messages/general*")
            .Respond(HttpStatusCode.OK, request => JsonContent.Create(messages));
        mockHttp.When(HttpMethod.Get, "*/api/channels/general/users")
            .Respond(HttpStatusCode.OK, request => JsonContent.Create(new List<User>()));

        var deleteRequest = mockHttp.When(HttpMethod.Delete, $"*/api/messages/general/{messageId}")
            .WithHeaders("Authorization", $"Bearer {testToken}")
            .Respond(HttpStatusCode.NoContent);

        chatServiceMock.Setup(x => x.InitializeAsync(It.IsAny<IHubConnectionBuilder>()))
            .Returns(Task.CompletedTask);
        chatServiceMock.Setup(x => x.JoinChannel("general")).Returns(Task.CompletedTask);
        privateMessageServiceMock.Setup(x => x.GetConversationsAsync(It.IsAny<string>()))
            .ReturnsAsync([]);

        var cut = await RenderChatAsync(channelName: "general");
        await Task.Delay(200);

        // Act
        await cut.InvokeAsync(() => cut.Instance.GetType()
            .GetMethod("HandleDeleteMessage", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .Invoke(cut.Instance, [messageId]));
        await Task.Delay(200);

        // Assert
        Assert.Equal(1, mockHttp.GetMatchCount(deleteRequest));
    }

    [Fact]
    public async Task Chat_HandleDeleteMessage_WhenTokenMissing_ShouldLogWarningAndReturn()
    {
        // Arrange
        SetupBasicAuth();
        authServiceMock.Setup(x => x.Token).Returns((string?)null);

        var channels = new List<Channel>
        {
            new() { Id = Guid.NewGuid(), Name = "general", CreatedBy = "system", CreatedAt = DateTime.UtcNow },
        };
        var messageId = Guid.NewGuid();

        mockHttp.When(HttpMethod.Get, "*/api/channels")
            .Respond(HttpStatusCode.OK, request => JsonContent.Create(channels));
        mockHttp.When(HttpMethod.Get, "*/api/my-channels?username=TestUser")
            .Respond(HttpStatusCode.OK, request => JsonContent.Create(channels));
        mockHttp.When(HttpMethod.Get, "*/api/messages/general*")
            .Respond(HttpStatusCode.OK, request => JsonContent.Create(new List<Message>()));
        mockHttp.When(HttpMethod.Get, "*/api/channels/general/users")
            .Respond(HttpStatusCode.OK, request => JsonContent.Create(new List<User>()));

        var deleteRequest = mockHttp.When(HttpMethod.Delete, $"*/api/messages/general/{messageId}")
            .Respond(HttpStatusCode.NoContent);

        chatServiceMock.Setup(x => x.InitializeAsync(It.IsAny<IHubConnectionBuilder>()))
            .Returns(Task.CompletedTask);
        chatServiceMock.Setup(x => x.JoinChannel("general")).Returns(Task.CompletedTask);
        privateMessageServiceMock.Setup(x => x.GetConversationsAsync(It.IsAny<string>()))
            .ReturnsAsync([]);

        var cut = await RenderChatAsync(channelName: "general");
        await Task.Delay(200);

        // Act
        var exception = await Record.ExceptionAsync(async () =>
        {
            await cut.InvokeAsync(() => cut.Instance.GetType()
                .GetMethod("HandleDeleteMessage", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
                .Invoke(cut.Instance, [messageId]));
            await Task.Delay(200);
        });

        // Assert - Ne devrait pas crasher et ne devrait pas appeler l'API
        Assert.Null(exception);
        Assert.Equal(0, mockHttp.GetMatchCount(deleteRequest));
    }

    [Fact]
    public async Task Chat_HandleDeleteMessage_WhenApiFails_ShouldLogError()
    {
        // Arrange
        SetupBasicAuth();
        authServiceMock.Setup(x => x.Token).Returns("test-token");

        var channels = new List<Channel>
        {
            new() { Id = Guid.NewGuid(), Name = "general", CreatedBy = "system", CreatedAt = DateTime.UtcNow },
        };
        var messageId = Guid.NewGuid();
        var messages = new List<Message>
        {
            new() { Id = messageId, Username = "User1", UserId = "User1", Content = "Test", Channel = "general", Timestamp = DateTime.UtcNow },
        };

        mockHttp.When(HttpMethod.Get, "*/api/channels")
            .Respond(HttpStatusCode.OK, request => JsonContent.Create(channels));
        mockHttp.When(HttpMethod.Get, "*/api/my-channels?username=TestUser")
            .Respond(HttpStatusCode.OK, request => JsonContent.Create(channels));
        mockHttp.When(HttpMethod.Get, "*/api/messages/general*")
            .Respond(HttpStatusCode.OK, request => JsonContent.Create(messages));
        mockHttp.When(HttpMethod.Get, "*/api/channels/general/users")
            .Respond(HttpStatusCode.OK, request => JsonContent.Create(new List<User>()));

        mockHttp.When(HttpMethod.Delete, $"*/api/messages/general/{messageId}")
            .Respond(HttpStatusCode.InternalServerError);

        chatServiceMock.Setup(x => x.InitializeAsync(It.IsAny<IHubConnectionBuilder>()))
            .Returns(Task.CompletedTask);
        chatServiceMock.Setup(x => x.JoinChannel("general")).Returns(Task.CompletedTask);
        privateMessageServiceMock.Setup(x => x.GetConversationsAsync(It.IsAny<string>()))
            .ReturnsAsync([]);

        var cut = await RenderChatAsync(channelName: "general");
        await Task.Delay(200);

        // Act
        var exception = await Record.ExceptionAsync(async () =>
        {
            await cut.InvokeAsync(() => cut.Instance.GetType()
                .GetMethod("HandleDeleteMessage", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
                .Invoke(cut.Instance, [messageId]));
            await Task.Delay(200);
        });

        // Assert - Ne devrait pas crasher malgré l'erreur API
        Assert.Null(exception);
    }

    [Fact]
    public async Task Chat_HandleDeleteMessage_WithDifferentStatusCodes_ShouldHandleGracefully()
    {
        // Arrange
        SetupBasicAuth();
        authServiceMock.Setup(x => x.Token).Returns("test-token");

        var channels = new List<Channel>
        {
            new() { Id = Guid.NewGuid(), Name = "general", CreatedBy = "system", CreatedAt = DateTime.UtcNow },
        };
        var messageId1 = Guid.NewGuid();
        var messageId2 = Guid.NewGuid();
        var messageId3 = Guid.NewGuid();

        mockHttp.When(HttpMethod.Get, "*/api/channels")
            .Respond(HttpStatusCode.OK, request => JsonContent.Create(channels));
        mockHttp.When(HttpMethod.Get, "*/api/my-channels?username=TestUser")
            .Respond(HttpStatusCode.OK, request => JsonContent.Create(channels));
        mockHttp.When(HttpMethod.Get, "*/api/messages/general*")
            .Respond(HttpStatusCode.OK, request => JsonContent.Create(new List<Message>()));
        mockHttp.When(HttpMethod.Get, "*/api/channels/general/users")
            .Respond(HttpStatusCode.OK, request => JsonContent.Create(new List<User>()));

        // Différents codes de statut d'erreur
        mockHttp.When(HttpMethod.Delete, $"*/api/messages/general/{messageId1}")
            .Respond(HttpStatusCode.Forbidden); // 403

        mockHttp.When(HttpMethod.Delete, $"*/api/messages/general/{messageId2}")
            .Respond(HttpStatusCode.NotFound); // 404

        mockHttp.When(HttpMethod.Delete, $"*/api/messages/general/{messageId3}")
            .Respond(HttpStatusCode.InternalServerError); // 500

        chatServiceMock.Setup(x => x.InitializeAsync(It.IsAny<IHubConnectionBuilder>()))
            .Returns(Task.CompletedTask);
        chatServiceMock.Setup(x => x.JoinChannel("general")).Returns(Task.CompletedTask);
        privateMessageServiceMock.Setup(x => x.GetConversationsAsync(It.IsAny<string>()))
            .ReturnsAsync([]);

        var cut = await RenderChatAsync(channelName: "general");
        await Task.Delay(200);

        // Act - Essayer de supprimer avec différents codes d'erreur
        var exception1 = await Record.ExceptionAsync(async () =>
        {
            await cut.InvokeAsync(() => cut.Instance.GetType()
                .GetMethod("HandleDeleteMessage", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
                .Invoke(cut.Instance, [messageId1]));
            await Task.Delay(100);
        });

        var exception2 = await Record.ExceptionAsync(async () =>
        {
            await cut.InvokeAsync(() => cut.Instance.GetType()
                .GetMethod("HandleDeleteMessage", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
                .Invoke(cut.Instance, [messageId2]));
            await Task.Delay(100);
        });

        var exception3 = await Record.ExceptionAsync(async () =>
        {
            await cut.InvokeAsync(() => cut.Instance.GetType()
                .GetMethod("HandleDeleteMessage", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
                .Invoke(cut.Instance, [messageId3]));
            await Task.Delay(100);
        });

        // Assert - Aucune exception ne devrait être levée
        Assert.Null(exception1);
        Assert.Null(exception2);
        Assert.Null(exception3);
    }

    [Fact]
    public async Task Chat_HandleDeleteMessage_ShouldNotRethrowException()
    {
        // Arrange
        SetupBasicAuth();
        authServiceMock.Setup(x => x.Token).Returns("test-token");

        var channels = new List<Channel>
        {
            new() { Id = Guid.NewGuid(), Name = "general", CreatedBy = "system", CreatedAt = DateTime.UtcNow },
        };
        var messageId = Guid.NewGuid();

        mockHttp.When(HttpMethod.Get, "*/api/channels")
            .Respond(HttpStatusCode.OK, request => JsonContent.Create(channels));
        mockHttp.When(HttpMethod.Get, "*/api/my-channels?username=TestUser")
            .Respond(HttpStatusCode.OK, request => JsonContent.Create(channels));
        mockHttp.When(HttpMethod.Get, "*/api/messages/general*")
            .Respond(HttpStatusCode.OK, request => JsonContent.Create(new List<Message>()));
        mockHttp.When(HttpMethod.Get, "*/api/channels/general/users")
            .Respond(HttpStatusCode.OK, request => JsonContent.Create(new List<User>()));

        // Simuler une exception réseau
        mockHttp.When(HttpMethod.Delete, $"*/api/messages/general/{messageId}")
            .Throw(new HttpRequestException("Network error"));

        chatServiceMock.Setup(x => x.InitializeAsync(It.IsAny<IHubConnectionBuilder>()))
            .Returns(Task.CompletedTask);
        chatServiceMock.Setup(x => x.JoinChannel("general")).Returns(Task.CompletedTask);
        privateMessageServiceMock.Setup(x => x.GetConversationsAsync(It.IsAny<string>()))
            .ReturnsAsync([]);

        var cut = await RenderChatAsync(channelName: "general");
        await Task.Delay(200);

        // Act
        var exception = await Record.ExceptionAsync(async () =>
        {
            await cut.InvokeAsync(() => cut.Instance.GetType()
                .GetMethod("HandleDeleteMessage", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
                .Invoke(cut.Instance, [messageId]));
            await Task.Delay(200);
        });

        // Assert - L'exception ne devrait pas être propagée
        Assert.Null(exception);
    }

    [Fact]
    public async Task Chat_InitializeSignalR_ShouldSubscribeToOnMessageDeletedEvent()
    {
        // Arrange
        SetupBasicAuth();
        SetupBasicMocks();

        // Act
        await RenderChatAsync();

        // Assert
        chatServiceMock.VerifyAdd(x => x.OnMessageDeleted += It.IsAny<Action<Guid, string>>());
    }

    [Fact]
    public async Task Chat_DisposeAsync_ShouldUnsubscribeFromOnMessageDeletedEvent()
    {
        // Arrange
        SetupBasicAuth();
        SetupBasicMocks();
        var cut = await RenderChatAsync();

        // Act
        await cut.Instance.DisposeAsync();

        // Assert
        chatServiceMock.VerifyRemove(x => x.OnMessageDeleted -= It.IsAny<Action<Guid, string>>());
    }

    [Fact]
    [SuppressMessage("Major Code Smell", "S6966:Awaitable method should be used", Justification = "Raise doesn't support async")]
    public async Task Chat_OnMessageDeleted_WithEmptyGuid_ShouldNotThrow()
    {
        // Arrange
        SetupBasicAuth();
        var channels = new List<Channel>
        {
            new() { Id = Guid.NewGuid(), Name = "general", CreatedBy = "system", CreatedAt = DateTime.UtcNow },
        };

        mockHttp.When(HttpMethod.Get, "*/api/channels")
            .Respond(HttpStatusCode.OK, request => JsonContent.Create(channels));
        mockHttp.When(HttpMethod.Get, "*/api/my-channels?username=TestUser")
            .Respond(HttpStatusCode.OK, request => JsonContent.Create(channels));
        mockHttp.When(HttpMethod.Get, "*/api/messages/general*")
            .Respond(HttpStatusCode.OK, request => JsonContent.Create(new List<Message>()));
        mockHttp.When(HttpMethod.Get, "*/api/channels/general/users")
            .Respond(HttpStatusCode.OK, request => JsonContent.Create(new List<User>()));

        chatServiceMock.Setup(x => x.InitializeAsync(It.IsAny<IHubConnectionBuilder>()))
            .Returns(Task.CompletedTask);
        chatServiceMock.Setup(x => x.JoinChannel("general")).Returns(Task.CompletedTask);
        privateMessageServiceMock.Setup(x => x.GetConversationsAsync(It.IsAny<string>()))
            .ReturnsAsync([]);

        var cut = await RenderChatAsync(channelName: "general");
        await Task.Delay(200);

        // Act
        var exception = await Record.ExceptionAsync(async () =>
        {
            chatServiceMock.Raise(x => x.OnMessageDeleted += null, Guid.Empty, "general");
            await Task.Delay(200);
        });

        // Assert
        Assert.Null(exception);
    }

    [Fact]
    public async Task Chat_HandleDeleteMessage_WhenTokenIsEmpty_ShouldNotCallApi()
    {
        // Arrange
        SetupBasicAuth();
        authServiceMock.Setup(x => x.Token).Returns(string.Empty);

        var channels = new List<Channel>
        {
            new() { Id = Guid.NewGuid(), Name = "general", CreatedBy = "system", CreatedAt = DateTime.UtcNow },
        };
        var messageId = Guid.NewGuid();

        mockHttp.When(HttpMethod.Get, "*/api/channels")
            .Respond(HttpStatusCode.OK, request => JsonContent.Create(channels));
        mockHttp.When(HttpMethod.Get, "*/api/my-channels?username=TestUser")
            .Respond(HttpStatusCode.OK, request => JsonContent.Create(channels));
        mockHttp.When(HttpMethod.Get, "*/api/messages/general*")
            .Respond(HttpStatusCode.OK, request => JsonContent.Create(new List<Message>()));
        mockHttp.When(HttpMethod.Get, "*/api/channels/general/users")
            .Respond(HttpStatusCode.OK, request => JsonContent.Create(new List<User>()));

        var deleteRequest = mockHttp.When(HttpMethod.Delete, $"*/api/messages/general/{messageId}")
            .Respond(HttpStatusCode.NoContent);

        chatServiceMock.Setup(x => x.InitializeAsync(It.IsAny<IHubConnectionBuilder>()))
            .Returns(Task.CompletedTask);
        chatServiceMock.Setup(x => x.JoinChannel("general")).Returns(Task.CompletedTask);
        privateMessageServiceMock.Setup(x => x.GetConversationsAsync(It.IsAny<string>()))
            .ReturnsAsync([]);

        var cut = await RenderChatAsync(channelName: "general");
        await Task.Delay(200);

        // Act
        await cut.InvokeAsync(() => cut.Instance.GetType()
            .GetMethod("HandleDeleteMessage", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .Invoke(cut.Instance, [messageId]));
        await Task.Delay(200);

        // Assert
        Assert.Equal(0, mockHttp.GetMatchCount(deleteRequest));
    }

    [Fact]
    [SuppressMessage("Major Code Smell", "S6966:Awaitable method should be used", Justification = "Raise doesn't support async")]
    public async Task Chat_OnMessageDeleted_SequentialDeletions_ShouldMaintainCorrectMessageList()
    {
        // Arrange
        SetupBasicAuth();
        var channels = new List<Channel>
        {
            new() { Id = Guid.NewGuid(), Name = "general", CreatedBy = "system", CreatedAt = DateTime.UtcNow },
        };
        var messageId1 = Guid.NewGuid();
        var messageId2 = Guid.NewGuid();
        var messages = new List<Message>
        {
            new() { Id = messageId1, Username = "User1", UserId = "User1", Content = "First", Channel = "general", Timestamp = DateTime.UtcNow },
            new() { Id = messageId2, Username = "User2", UserId = "User2", Content = "Second", Channel = "general", Timestamp = DateTime.UtcNow },
        };

        mockHttp.When(HttpMethod.Get, "*/api/channels")
            .Respond(HttpStatusCode.OK, request => JsonContent.Create(channels));
        mockHttp.When(HttpMethod.Get, "*/api/my-channels?username=TestUser")
            .Respond(HttpStatusCode.OK, request => JsonContent.Create(channels));
        mockHttp.When(HttpMethod.Get, "*/api/messages/general*")
            .Respond(HttpStatusCode.OK, request => JsonContent.Create(messages));
        mockHttp.When(HttpMethod.Get, "*/api/channels/general/users")
            .Respond(HttpStatusCode.OK, request => JsonContent.Create(new List<User>()));

        chatServiceMock.Setup(x => x.InitializeAsync(It.IsAny<IHubConnectionBuilder>()))
            .Returns(Task.CompletedTask);
        chatServiceMock.Setup(x => x.JoinChannel("general")).Returns(Task.CompletedTask);
        privateMessageServiceMock.Setup(x => x.GetConversationsAsync(It.IsAny<string>()))
            .ReturnsAsync([]);

        var cut = await RenderChatAsync(channelName: "general");
        await Task.Delay(200);
        cut.Render();

        // Act & Assert - Première suppression
        chatServiceMock.Raise(x => x.OnMessageDeleted += null, messageId1, "general");
        await Task.Delay(100);
        cut.Render();
        Assert.DoesNotContain("First", cut.Markup);
        Assert.Contains("Second", cut.Markup);

        // Act & Assert - Deuxième suppression
        chatServiceMock.Raise(x => x.OnMessageDeleted += null, messageId2, "general");
        await Task.Delay(100);
        cut.Render();
        Assert.DoesNotContain("First", cut.Markup);
        Assert.DoesNotContain("Second", cut.Markup);
    }
}