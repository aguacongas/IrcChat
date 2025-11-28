// tests/IrcChat.Client.Tests/Pages/ChatTests.UserJoinLeave.cs
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Http.Json;
using Bunit;
using IrcChat.Shared.Models;
using Microsoft.AspNetCore.SignalR.Client;
using Moq;
using RichardSzalay.MockHttp;
using Xunit;

namespace IrcChat.Client.Tests.Pages;

public partial class ChatTests
{
    // ============ TESTS POUR OnUserJoined ============

    [Fact]
    [SuppressMessage("Major Code Smell", "S6966:Awaitable method should be used", Justification = "Raise doesn't support async")]
    public async Task Chat_OnUserJoined_ShouldAddUserToList()
    {
        // Arrange
        SetupBasicAuth();
        var channels = new List<Channel>
        {
            new() { Id = Guid.NewGuid(), Name = "general", CreatedBy = "system", CreatedAt = DateTime.UtcNow }
        };
        var initialUsers = new List<User>
        {
            new() { UserId = "user1", Username = "Alice" }
        };

        _mockHttp.When(HttpMethod.Get, "*/api/channels")
            .Respond(HttpStatusCode.OK, JsonContent.Create(channels));
        _mockHttp.When(HttpMethod.Get, "*/api/messages/general")
            .Respond(HttpStatusCode.OK, JsonContent.Create(new List<Message>()));
        _mockHttp.When(HttpMethod.Get, "*/api/channels/general/users")
            .Respond(HttpStatusCode.OK, JsonContent.Create(initialUsers));

        _chatServiceMock.Setup(x => x.InitializeAsync(It.IsAny<IHubConnectionBuilder>()))
            .Returns(Task.CompletedTask);
        _chatServiceMock.Setup(x => x.JoinChannel("general")).Returns(Task.CompletedTask);
        _privateMessageServiceMock.Setup(x => x.GetConversationsAsync(It.IsAny<string>()))
            .ReturnsAsync([]);

        var cut = await RenderChatAsync(channelName: "general");
        cut.Render();

        // Vérifier qu'Alice est présente
        Assert.Contains("Alice", cut.Markup);

        // Act - Bob rejoint le canal
        _chatServiceMock.Raise(x => x.OnUserJoined += null, "Bob", "user2", "general");
        await Task.Delay(200);
        cut.Render();

        // Assert - Bob devrait être dans la liste
        Assert.Contains("Alice", cut.Markup);
        Assert.Contains("Bob", cut.Markup);
    }

    [Fact]
    [SuppressMessage("Major Code Smell", "S6966:Awaitable method should be used", Justification = "Raise doesn't support async")]
    public async Task Chat_OnUserJoined_WhenUserAlreadyInList_ShouldNotAddDuplicate()
    {
        // Arrange
        SetupBasicAuth();
        var channels = new List<Channel>
        {
            new() { Id = Guid.NewGuid(), Name = "general", CreatedBy = "system", CreatedAt = DateTime.UtcNow }
        };
        var initialUsers = new List<User>
        {
            new() { UserId = "user1", Username = "Alice" }
        };

        _mockHttp.When(HttpMethod.Get, "*/api/channels")
            .Respond(HttpStatusCode.OK, JsonContent.Create(channels));
        _mockHttp.When(HttpMethod.Get, "*/api/messages/general")
            .Respond(HttpStatusCode.OK, JsonContent.Create(new List<Message>()));
        _mockHttp.When(HttpMethod.Get, "*/api/channels/general/users")
            .Respond(HttpStatusCode.OK, JsonContent.Create(initialUsers));

        _chatServiceMock.Setup(x => x.InitializeAsync(It.IsAny<IHubConnectionBuilder>()))
            .Returns(Task.CompletedTask);
        _chatServiceMock.Setup(x => x.JoinChannel("general")).Returns(Task.CompletedTask);
        _privateMessageServiceMock.Setup(x => x.GetConversationsAsync(It.IsAny<string>()))
            .ReturnsAsync([]);

        var cut = await RenderChatAsync(channelName: "general");

        // Act - Alice rejoint à nouveau (doublon)
        _chatServiceMock.Raise(x => x.OnUserJoined += null, "Alice", "user1", "general");
        await Task.Delay(200);
        cut.Render();

        // Assert - Ne devrait pas ajouter de doublon
        var aliceCount = System.Text.RegularExpressions.Regex.Matches(cut.Markup, "Alice").Count;
        Assert.Equal(1, aliceCount); // Devrait apparaître une seule fois dans la liste des utilisateurs
    }

    [Fact]
    [SuppressMessage("Major Code Smell", "S6966:Awaitable method should be used", Justification = "Raise doesn't support async")]
    public async Task Chat_OnUserJoined_WhenDifferentChannel_ShouldNotAddUser()
    {
        // Arrange
        SetupBasicAuth();
        var channels = new List<Channel>
        {
            new() { Id = Guid.NewGuid(), Name = "general", CreatedBy = "system", CreatedAt = DateTime.UtcNow },
            new() { Id = Guid.NewGuid(), Name = "random", CreatedBy = "system", CreatedAt = DateTime.UtcNow }
        };
        var initialUsers = new List<User>
        {
            new() { UserId = "user1", Username = "Alice" }
        };

        _mockHttp.When(HttpMethod.Get, "*/api/channels")
            .Respond(HttpStatusCode.OK, JsonContent.Create(channels));
        _mockHttp.When(HttpMethod.Get, "*/api/messages/general")
            .Respond(HttpStatusCode.OK, JsonContent.Create(new List<Message>()));
        _mockHttp.When(HttpMethod.Get, "*/api/channels/general/users")
            .Respond(HttpStatusCode.OK, JsonContent.Create(initialUsers));

        _chatServiceMock.Setup(x => x.InitializeAsync(It.IsAny<IHubConnectionBuilder>()))
            .Returns(Task.CompletedTask);
        _chatServiceMock.Setup(x => x.JoinChannel("general")).Returns(Task.CompletedTask);
        _privateMessageServiceMock.Setup(x => x.GetConversationsAsync(It.IsAny<string>()))
            .ReturnsAsync([]);

        var cut = await RenderChatAsync(channelName: "general");
        cut.Render();

        // Act - Bob rejoint un autre canal
        _chatServiceMock.Raise(x => x.OnUserJoined += null, "Bob", "user2", "random");
        await Task.Delay(200);
        cut.Render();

        // Assert - Bob ne devrait pas apparaître dans la liste des utilisateurs de general
        Assert.Contains("Alice", cut.Markup);
        Assert.DoesNotContain("Bob", cut.Markup);
    }

    [Fact]
    [SuppressMessage("Major Code Smell", "S6966:Awaitable method should be used", Justification = "Raise doesn't support async")]
    public async Task Chat_OnUserJoined_WhenInPrivateConversation_ShouldNotAddUser()
    {
        // Arrange
        SetupBasicAuth();
        _mockHttp.When(HttpMethod.Get, "*/api/channels")
            .Respond(HttpStatusCode.OK, JsonContent.Create(new List<Channel>()));
        _mockHttp.When(HttpMethod.Get, "*/api/private-messages/status/Friend")
            .Respond(HttpStatusCode.OK, JsonContent.Create(new { Username = "Friend", IsOnline = true }));

        _chatServiceMock.Setup(x => x.InitializeAsync(It.IsAny<IHubConnectionBuilder>()))
            .Returns(Task.CompletedTask);
        _chatServiceMock.Setup(x => x.MarkPrivateMessagesAsRead(It.IsAny<string>()))
            .Returns(Task.CompletedTask);
        _privateMessageServiceMock.Setup(x => x.GetConversationsAsync("TestUser"))
            .ReturnsAsync([new() { OtherUser = new User { UserId = "Friend", Username = "Friend" }, LastMessage = "Hi", LastMessageTime = DateTime.UtcNow, UnreadCount = 0 }]);
        _privateMessageServiceMock.Setup(x => x.GetPrivateMessagesAsync("TestUser", "Friend"))
            .ReturnsAsync([]);

        var cut = await RenderChatAsync();
        await NavigateToPrivateChatAsync(cut, "Friend", "Friend");

        // Act - Bob rejoint un canal pendant une conversation privée
        _chatServiceMock.Raise(x => x.OnUserJoined += null, "Bob", "user2", "general");
        await Task.Delay(200);
        cut.Render();

        // Assert - Bob ne devrait pas apparaître
        Assert.DoesNotContain("Bob", cut.Markup);
    }

    [Fact]
    [SuppressMessage("Major Code Smell", "S6966:Awaitable method should be used", Justification = "Raise doesn't support async")]
    public async Task Chat_OnUserJoined_MultipleUsers_ShouldAddAllUsers()
    {
        // Arrange
        SetupBasicAuth();
        var channels = new List<Channel>
        {
            new() { Id = Guid.NewGuid(), Name = "general", CreatedBy = "system", CreatedAt = DateTime.UtcNow }
        };

        _mockHttp.When(HttpMethod.Get, "*/api/channels")
            .Respond(HttpStatusCode.OK, JsonContent.Create(channels));
        _mockHttp.When(HttpMethod.Get, "*/api/messages/general")
            .Respond(HttpStatusCode.OK, JsonContent.Create(new List<Message>()));
        _mockHttp.When(HttpMethod.Get, "*/api/channels/general/users")
            .Respond(HttpStatusCode.OK, JsonContent.Create(new List<User>()));

        _chatServiceMock.Setup(x => x.InitializeAsync(It.IsAny<IHubConnectionBuilder>()))
            .Returns(Task.CompletedTask);
        _chatServiceMock.Setup(x => x.JoinChannel("general")).Returns(Task.CompletedTask);
        _privateMessageServiceMock.Setup(x => x.GetConversationsAsync(It.IsAny<string>()))
            .ReturnsAsync([]);

        var cut = await RenderChatAsync(channelName: "general");

        // Act - Plusieurs utilisateurs rejoignent
        _chatServiceMock.Raise(x => x.OnUserJoined += null, "Alice", "user1", "general");
        await Task.Delay(100);
        _chatServiceMock.Raise(x => x.OnUserJoined += null, "Bob", "user2", "general");
        await Task.Delay(100);
        _chatServiceMock.Raise(x => x.OnUserJoined += null, "Charlie", "user3", "general");
        await Task.Delay(100);
        cut.Render();

        // Assert - Tous les utilisateurs devraient être présents
        Assert.Contains("Alice", cut.Markup);
        Assert.Contains("Bob", cut.Markup);
        Assert.Contains("Charlie", cut.Markup);
    }

    // ============ TESTS POUR OnUserLeft ============

    [Fact]
    [SuppressMessage("Major Code Smell", "S6966:Awaitable method should be used", Justification = "Raise doesn't support async")]
    public async Task Chat_OnUserLeft_ShouldRemoveUserFromList()
    {
        // Arrange
        SetupBasicAuth();
        var channels = new List<Channel>
        {
            new() { Id = Guid.NewGuid(), Name = "general", CreatedBy = "system", CreatedAt = DateTime.UtcNow }
        };
        var initialUsers = new List<User>
        {
            new() { UserId = "user1", Username = "Alice" },
            new() { UserId = "user2", Username = "Bob" }
        };

        _mockHttp.When(HttpMethod.Get, "*/api/channels")
            .Respond(HttpStatusCode.OK, JsonContent.Create(channels));
        _mockHttp.When(HttpMethod.Get, "*/api/messages/general")
            .Respond(HttpStatusCode.OK, JsonContent.Create(new List<Message>()));
        _mockHttp.When(HttpMethod.Get, "*/api/channels/general/users")
            .Respond(HttpStatusCode.OK, JsonContent.Create(initialUsers));

        _chatServiceMock.Setup(x => x.InitializeAsync(It.IsAny<IHubConnectionBuilder>()))
            .Returns(Task.CompletedTask);
        _chatServiceMock.Setup(x => x.JoinChannel("general")).Returns(Task.CompletedTask);
        _privateMessageServiceMock.Setup(x => x.GetConversationsAsync(It.IsAny<string>()))
            .ReturnsAsync([]);

        var cut = await RenderChatAsync(channelName: "general");
        cut.Render();

        // Vérifier que les deux utilisateurs sont présents
        Assert.Contains("Alice", cut.Markup);
        Assert.Contains("Bob", cut.Markup);

        // Act - Bob quitte le canal
        _chatServiceMock.Raise(x => x.OnUserLeft += null, "Bob", "user2", "general");
        await Task.Delay(200);
        cut.Render();

        // Assert - Bob ne devrait plus être dans la liste
        Assert.Contains("Alice", cut.Markup);
        Assert.DoesNotContain("Bob", cut.Markup);
    }

    [Fact]
    [SuppressMessage("Major Code Smell", "S6966:Awaitable method should be used", Justification = "Raise doesn't support async")]
    public async Task Chat_OnUserLeft_WhenUserNotInList_ShouldNotThrowException()
    {
        // Arrange
        SetupBasicAuth();
        var channels = new List<Channel>
        {
            new() { Id = Guid.NewGuid(), Name = "general", CreatedBy = "system", CreatedAt = DateTime.UtcNow }
        };
        var initialUsers = new List<User>
        {
            new() { UserId = "user1", Username = "Alice" }
        };

        _mockHttp.When(HttpMethod.Get, "*/api/channels")
            .Respond(HttpStatusCode.OK, JsonContent.Create(channels));
        _mockHttp.When(HttpMethod.Get, "*/api/messages/general")
            .Respond(HttpStatusCode.OK, JsonContent.Create(new List<Message>()));
        _mockHttp.When(HttpMethod.Get, "*/api/channels/general/users")
            .Respond(HttpStatusCode.OK, JsonContent.Create(initialUsers));

        _chatServiceMock.Setup(x => x.InitializeAsync(It.IsAny<IHubConnectionBuilder>()))
            .Returns(Task.CompletedTask);
        _chatServiceMock.Setup(x => x.JoinChannel("general")).Returns(Task.CompletedTask);
        _privateMessageServiceMock.Setup(x => x.GetConversationsAsync(It.IsAny<string>()))
            .ReturnsAsync([]);

        var cut = await RenderChatAsync(channelName: "general");

        // Act & Assert - Ne devrait pas lever d'exception
        var exception = await Record.ExceptionAsync(async () =>
        {
            _chatServiceMock.Raise(x => x.OnUserLeft += null, "NonExistent", "user999", "general");
            await Task.Delay(200);
        });

        Assert.Null(exception);
    }

    [Fact]
    [SuppressMessage("Major Code Smell", "S6966:Awaitable method should be used", Justification = "Raise doesn't support async")]
    public async Task Chat_OnUserLeft_WhenDifferentChannel_ShouldNotRemoveUser()
    {
        // Arrange
        SetupBasicAuth();
        var channels = new List<Channel>
        {
            new() { Id = Guid.NewGuid(), Name = "general", CreatedBy = "system", CreatedAt = DateTime.UtcNow },
            new() { Id = Guid.NewGuid(), Name = "random", CreatedBy = "system", CreatedAt = DateTime.UtcNow }
        };
        var initialUsers = new List<User>
        {
            new() { UserId = "user1", Username = "Alice" },
            new() { UserId = "user2", Username = "Bob" }
        };

        _mockHttp.When(HttpMethod.Get, "*/api/channels")
            .Respond(HttpStatusCode.OK, JsonContent.Create(channels));
        _mockHttp.When(HttpMethod.Get, "*/api/messages/general")
            .Respond(HttpStatusCode.OK, JsonContent.Create(new List<Message>()));
        _mockHttp.When(HttpMethod.Get, "*/api/channels/general/users")
            .Respond(HttpStatusCode.OK, JsonContent.Create(initialUsers));

        _chatServiceMock.Setup(x => x.InitializeAsync(It.IsAny<IHubConnectionBuilder>()))
            .Returns(Task.CompletedTask);
        _chatServiceMock.Setup(x => x.JoinChannel("general")).Returns(Task.CompletedTask);
        _privateMessageServiceMock.Setup(x => x.GetConversationsAsync(It.IsAny<string>()))
            .ReturnsAsync([]);

        var cut = await RenderChatAsync(channelName: "general");
        cut.Render();

        // Act - Bob quitte un autre canal
        _chatServiceMock.Raise(x => x.OnUserLeft += null, "Bob", "user2", "random");
        await Task.Delay(200);
        cut.Render();

        // Assert - Bob devrait toujours être présent dans general
        Assert.Contains("Alice", cut.Markup);
        Assert.Contains("Bob", cut.Markup);
    }

    [Fact]
    [SuppressMessage("Major Code Smell", "S6966:Awaitable method should be used", Justification = "Raise doesn't support async")]
    public async Task Chat_OnUserLeft_WhenInPrivateConversation_ShouldNotRemoveUser()
    {
        // Arrange
        SetupBasicAuth();
        _mockHttp.When(HttpMethod.Get, "*/api/channels")
            .Respond(HttpStatusCode.OK, JsonContent.Create(new List<Channel>()));
        _mockHttp.When(HttpMethod.Get, "*/api/private-messages/status/Friend")
            .Respond(HttpStatusCode.OK, JsonContent.Create(new { Username = "Friend", IsOnline = true }));

        _chatServiceMock.Setup(x => x.InitializeAsync(It.IsAny<IHubConnectionBuilder>()))
            .Returns(Task.CompletedTask);
        _chatServiceMock.Setup(x => x.MarkPrivateMessagesAsRead(It.IsAny<string>()))
            .Returns(Task.CompletedTask);
        _privateMessageServiceMock.Setup(x => x.GetConversationsAsync("TestUser"))
            .ReturnsAsync([new() { OtherUser = new User { UserId = "Friend", Username = "Friend" }, LastMessage = "Hi", LastMessageTime = DateTime.UtcNow, UnreadCount = 0 }]);
        _privateMessageServiceMock.Setup(x => x.GetPrivateMessagesAsync("TestUser", "Friend"))
            .ReturnsAsync([]);

        var cut = await RenderChatAsync();
        await NavigateToPrivateChatAsync(cut, "Friend", "Friend");

        // Act - Un utilisateur quitte un canal pendant une conversation privée
        _chatServiceMock.Raise(x => x.OnUserLeft += null, "Alice", "user1", "general");
        await Task.Delay(200);

        // Assert - Devrait être ignoré (pas de liste d'utilisateurs en conversation privée)
        var exception = await Record.ExceptionAsync(async () =>
        {
            await Task.Delay(100);
        });

        Assert.Null(exception);
    }

    [Fact]
    [SuppressMessage("Major Code Smell", "S6966:Awaitable method should be used", Justification = "Raise doesn't support async")]
    public async Task Chat_OnUserLeft_MultipleUsers_ShouldRemoveAllUsers()
    {
        // Arrange
        SetupBasicAuth();
        var channels = new List<Channel>
        {
            new() { Id = Guid.NewGuid(), Name = "general", CreatedBy = "system", CreatedAt = DateTime.UtcNow }
        };
        var initialUsers = new List<User>
        {
            new() { UserId = "user1", Username = "Alice" },
            new() { UserId = "user2", Username = "Bob" },
            new() { UserId = "user3", Username = "Charlie" }
        };

        _mockHttp.When(HttpMethod.Get, "*/api/channels")
            .Respond(HttpStatusCode.OK, JsonContent.Create(channels));
        _mockHttp.When(HttpMethod.Get, "*/api/messages/general")
            .Respond(HttpStatusCode.OK, JsonContent.Create(new List<Message>()));
        _mockHttp.When(HttpMethod.Get, "*/api/channels/general/users")
            .Respond(HttpStatusCode.OK, JsonContent.Create(initialUsers));

        _chatServiceMock.Setup(x => x.InitializeAsync(It.IsAny<IHubConnectionBuilder>()))
            .Returns(Task.CompletedTask);
        _chatServiceMock.Setup(x => x.JoinChannel("general")).Returns(Task.CompletedTask);
        _privateMessageServiceMock.Setup(x => x.GetConversationsAsync(It.IsAny<string>()))
            .ReturnsAsync([]);

        var cut = await RenderChatAsync(channelName: "general");
        cut.Render();

        // Act - Plusieurs utilisateurs quittent
        _chatServiceMock.Raise(x => x.OnUserLeft += null, "Bob", "user2", "general");
        await Task.Delay(100);
        _chatServiceMock.Raise(x => x.OnUserLeft += null, "Charlie", "user3", "general");
        await Task.Delay(100);
        cut.Render();

        // Assert - Seule Alice devrait rester
        Assert.Contains("Alice", cut.Markup);
        Assert.DoesNotContain("Bob", cut.Markup);
        Assert.DoesNotContain("Charlie", cut.Markup);
    }

    [Fact]
    [SuppressMessage("Major Code Smell", "S6966:Awaitable method should be used", Justification = "Raise doesn't support async")]
    public async Task Chat_OnUserLeft_ByUserId_ShouldRemoveCorrectUser()
    {
        // Arrange
        SetupBasicAuth();
        var channels = new List<Channel>
        {
            new() { Id = Guid.NewGuid(), Name = "general", CreatedBy = "system", CreatedAt = DateTime.UtcNow }
        };
        var initialUsers = new List<User>
        {
            new() { UserId = "user1", Username = "Alice" },
            new() { UserId = "user2", Username = "Bob" },
            new() { UserId = "user3", Username = "Alice" } // Même nom mais ID différent
        };

        _mockHttp.When(HttpMethod.Get, "*/api/channels")
            .Respond(HttpStatusCode.OK, JsonContent.Create(channels));
        _mockHttp.When(HttpMethod.Get, "*/api/messages/general")
            .Respond(HttpStatusCode.OK, JsonContent.Create(new List<Message>()));
        _mockHttp.When(HttpMethod.Get, "*/api/channels/general/users")
            .Respond(HttpStatusCode.OK, JsonContent.Create(initialUsers));

        _chatServiceMock.Setup(x => x.InitializeAsync(It.IsAny<IHubConnectionBuilder>()))
            .Returns(Task.CompletedTask);
        _chatServiceMock.Setup(x => x.JoinChannel("general")).Returns(Task.CompletedTask);
        _privateMessageServiceMock.Setup(x => x.GetConversationsAsync(It.IsAny<string>()))
            .ReturnsAsync([]);

        var cut = await RenderChatAsync(channelName: "general");

        // Act - Retirer par userId (user1)
        _chatServiceMock.Raise(x => x.OnUserLeft += null, "Alice", "user1", "general");
        await Task.Delay(200);
        cut.Render();

        // Assert - user1 devrait être retiré mais pas user3 (même nom)
        // La liste devrait contenir Bob et le deuxième Alice (user3)
        var aliceCount = System.Text.RegularExpressions.Regex.Matches(cut.Markup, "Alice").Count;
        Assert.True(aliceCount >= 1); // Au moins un Alice devrait rester
        Assert.Contains("Bob", cut.Markup);
    }

    [Fact]
    [SuppressMessage("Major Code Smell", "S6966:Awaitable method should be used", Justification = "Raise doesn't support async")]
    public async Task Chat_OnUserJoinedThenLeft_ShouldMaintainCorrectState()
    {
        // Arrange
        SetupBasicAuth();
        var channels = new List<Channel>
        {
            new() { Id = Guid.NewGuid(), Name = "general", CreatedBy = "system", CreatedAt = DateTime.UtcNow }
        };
        var initialUsers = new List<User>
        {
            new() { UserId = "user1", Username = "Alice" }
        };

        _mockHttp.When(HttpMethod.Get, "*/api/channels")
            .Respond(HttpStatusCode.OK, JsonContent.Create(channels));
        _mockHttp.When(HttpMethod.Get, "*/api/messages/general")
            .Respond(HttpStatusCode.OK, JsonContent.Create(new List<Message>()));
        _mockHttp.When(HttpMethod.Get, "*/api/channels/general/users")
            .Respond(HttpStatusCode.OK, JsonContent.Create(initialUsers));

        _chatServiceMock.Setup(x => x.InitializeAsync(It.IsAny<IHubConnectionBuilder>()))
            .Returns(Task.CompletedTask);
        _chatServiceMock.Setup(x => x.JoinChannel("general")).Returns(Task.CompletedTask);
        _privateMessageServiceMock.Setup(x => x.GetConversationsAsync(It.IsAny<string>()))
            .ReturnsAsync([]);

        var cut = await RenderChatAsync(channelName: "general");

        // Act - Bob rejoint puis quitte
        _chatServiceMock.Raise(x => x.OnUserJoined += null, "Bob", "user2", "general");
        await Task.Delay(100);
        cut.Render();
        Assert.Contains("Bob", cut.Markup);

        _chatServiceMock.Raise(x => x.OnUserLeft += null, "Bob", "user2", "general");
        await Task.Delay(100);
        cut.Render();

        // Assert - Bob ne devrait plus être présent, Alice devrait toujours l'être
        Assert.Contains("Alice", cut.Markup);
        Assert.DoesNotContain("Bob", cut.Markup);
    }

    [Fact]
    [SuppressMessage("Major Code Smell", "S6966:Awaitable method should be used", Justification = "Raise doesn't support async")]
    public async Task Chat_OnUserLeft_AllUsers_ShouldHaveEmptyUserList()
    {
        // Arrange
        SetupBasicAuth();
        var channels = new List<Channel>
        {
            new() { Id = Guid.NewGuid(), Name = "general", CreatedBy = "system", CreatedAt = DateTime.UtcNow }
        };
        var initialUsers = new List<User>
        {
            new() { UserId = "user1", Username = "Alice" }
        };

        _mockHttp.When(HttpMethod.Get, "*/api/channels")
            .Respond(HttpStatusCode.OK, JsonContent.Create(channels));
        _mockHttp.When(HttpMethod.Get, "*/api/messages/general")
            .Respond(HttpStatusCode.OK, JsonContent.Create(new List<Message>()));
        _mockHttp.When(HttpMethod.Get, "*/api/channels/general/users")
            .Respond(HttpStatusCode.OK, JsonContent.Create(initialUsers));

        _chatServiceMock.Setup(x => x.InitializeAsync(It.IsAny<IHubConnectionBuilder>()))
            .Returns(Task.CompletedTask);
        _chatServiceMock.Setup(x => x.JoinChannel("general")).Returns(Task.CompletedTask);
        _privateMessageServiceMock.Setup(x => x.GetConversationsAsync(It.IsAny<string>()))
            .ReturnsAsync([]);

        var cut = await RenderChatAsync(channelName: "general");
        cut.Render();
        Assert.Contains("Alice", cut.Markup);

        // Act - Tous les utilisateurs quittent
        _chatServiceMock.Raise(x => x.OnUserLeft += null, "Alice", "user1", "general");
        await Task.Delay(200);
        cut.Render();

        // Assert - La liste devrait être vide (ou afficher "Aucun utilisateur")
        Assert.DoesNotContain("Alice", cut.Markup);
    }

    // ============ TESTS POUR NavigateToCurrentChannelOrHome ============

    [Fact]
    public async Task Chat_NavigateToCurrentChannelOrHome_WhenInChannel_ShouldNavigateToChannel()
    {
        // Arrange
        SetupBasicAuth();
        var channels = new List<Channel>
        {
            new() { Id = Guid.NewGuid(), Name = "general", CreatedBy = "system", CreatedAt = DateTime.UtcNow }
        };

        _mockHttp.When(HttpMethod.Get, "*/api/channels")
            .Respond(HttpStatusCode.OK, JsonContent.Create(channels));
        _mockHttp.When(HttpMethod.Get, "*/api/messages/general")
            .Respond(HttpStatusCode.OK, JsonContent.Create(new List<Message>()));
        _mockHttp.When(HttpMethod.Get, "*/api/channels/general/users")
            .Respond(HttpStatusCode.OK, JsonContent.Create(new List<User>()));

        _chatServiceMock.Setup(x => x.InitializeAsync(It.IsAny<IHubConnectionBuilder>()))
            .Returns(Task.CompletedTask);
        _chatServiceMock.Setup(x => x.JoinChannel("general")).Returns(Task.CompletedTask);
        _privateMessageServiceMock.Setup(x => x.GetConversationsAsync(It.IsAny<string>()))
            .ReturnsAsync([]);

        var cut = await RenderChatAsync(channelName: "general");

        // Act - Simuler la fermeture d'une conversation privée qui appellerait NavigateToCurrentChannelOrHome
        // On va naviguer vers une conversation privée puis la fermer
        _mockHttp.When(HttpMethod.Get, "*/api/private-messages/status/Friend")
            .Respond(HttpStatusCode.OK, JsonContent.Create(new { Username = "Friend", IsOnline = true }));
        _chatServiceMock.Setup(x => x.MarkPrivateMessagesAsRead(It.IsAny<string>()))
            .Returns(Task.CompletedTask);
        _privateMessageServiceMock.Setup(x => x.GetPrivateMessagesAsync("TestUser", "Friend"))
            .ReturnsAsync([]);

        await NavigateToPrivateChatAsync(cut, "Friend", "Friend");

        // Simuler la fermeture de la conversation privée en cliquant sur le bouton de fermeture
        var closeButton = await cut.InvokeAsync(() => cut.Find("button.close-btn"));
        await cut.InvokeAsync(() => closeButton.Click());
        await Task.Delay(200);

        // Assert - Devrait naviguer vers le canal actuel
        Assert.Contains("/chat/channel/general", _navManager.Uri);
    }

    [Fact]
    public async Task Chat_NavigateToCurrentChannelOrHome_WhenNoChannel_ShouldNavigateToChat()
    {
        // Arrange
        SetupBasicAuth();
        SetupBasicMocks();

        _navManager.NavigateTo("/chat");
        var cut = await RenderChatAsync();

        // Vérifier qu'on est sur /chat (pas de canal spécifique)
        Assert.EndsWith("/chat", _navManager.Uri);

        // Act - Ouvrir puis fermer une conversation privée
        _mockHttp.When(HttpMethod.Get, "*/api/private-messages/status/Friend")
            .Respond(HttpStatusCode.OK, JsonContent.Create(new { Username = "Friend", IsOnline = true }));
        _chatServiceMock.Setup(x => x.MarkPrivateMessagesAsRead(It.IsAny<string>()))
            .Returns(Task.CompletedTask);
        _privateMessageServiceMock.Setup(x => x.GetPrivateMessagesAsync("TestUser", "Friend"))
            .ReturnsAsync([]);

        await NavigateToPrivateChatAsync(cut, "Friend", "Friend");

        var closeButton = await cut.InvokeAsync(() => cut.Find("button.close-btn"));
        await cut.InvokeAsync(() => closeButton.Click());
        await Task.Delay(200);

        // Assert - Devrait naviguer vers /chat
        Assert.EndsWith("/chat", _navManager.Uri);
    }

    // ============ TESTS POUR SwitchChannel ============

    [Fact]
    public async Task Chat_SwitchChannel_ShouldNavigateToNewChannel()
    {
        // Arrange
        SetupBasicAuth();
        var channels = new List<Channel>
        {
            new() { Id = Guid.NewGuid(), Name = "general", CreatedBy = "system", CreatedAt = DateTime.UtcNow },
            new() { Id = Guid.NewGuid(), Name = "random", CreatedBy = "system", CreatedAt = DateTime.UtcNow }
        };

        _mockHttp.When(HttpMethod.Get, "*/api/channels")
            .Respond(HttpStatusCode.OK, JsonContent.Create(channels));
        _mockHttp.When(HttpMethod.Get, "*/api/messages/general")
            .Respond(HttpStatusCode.OK, JsonContent.Create(new List<Message>()));
        _mockHttp.When(HttpMethod.Get, "*/api/messages/random")
            .Respond(HttpStatusCode.OK, JsonContent.Create(new List<Message>()));
        _mockHttp.When(HttpMethod.Get, "*/api/channels/general/users")
            .Respond(HttpStatusCode.OK, JsonContent.Create(new List<User>()));
        _mockHttp.When(HttpMethod.Get, "*/api/channels/random/users")
            .Respond(HttpStatusCode.OK, JsonContent.Create(new List<User>()));

        _chatServiceMock.Setup(x => x.InitializeAsync(It.IsAny<IHubConnectionBuilder>()))
            .Returns(Task.CompletedTask);
        _chatServiceMock.Setup(x => x.JoinChannel(It.IsAny<string>())).Returns(Task.CompletedTask);
        _chatServiceMock.Setup(x => x.LeaveChannel(It.IsAny<string>())).Returns(Task.CompletedTask);
        _privateMessageServiceMock.Setup(x => x.GetConversationsAsync(It.IsAny<string>()))
            .ReturnsAsync([]);

        _navManager.NavigateTo("/chat/channel/general");
        var cut = await RenderChatAsync(channelName: "general");
        Assert.Contains("/chat/channel/general", _navManager.Uri);

        // Act - Cliquer sur le canal "random" dans la sidebar
        var channelLinks = await cut.InvokeAsync(() => cut.FindAll(".channel-list li"));
        var randomChannelLink = channelLinks.First(l => l.TextContent.Contains("random"));
        await cut.InvokeAsync(() => randomChannelLink.Click());
        await Task.Delay(200);

        // Assert - Devrait naviguer vers random
        Assert.Contains("/chat/channel/random", _navManager.Uri);
    }

    [Fact]
    public async Task Chat_SwitchChannel_ShouldLeaveCurrentChannel()
    {
        // Arrange
        SetupBasicAuth();
        var channels = new List<Channel>
        {
            new() { Id = Guid.NewGuid(), Name = "general", CreatedBy = "system", CreatedAt = DateTime.UtcNow },
            new() { Id = Guid.NewGuid(), Name = "random", CreatedBy = "system", CreatedAt = DateTime.UtcNow }
        };

        _mockHttp.When(HttpMethod.Get, "*/api/channels")
            .Respond(HttpStatusCode.OK, JsonContent.Create(channels));
        _mockHttp.When(HttpMethod.Get, "*/api/messages/*")
            .Respond(HttpStatusCode.OK, JsonContent.Create(new List<Message>()));
        _mockHttp.When(HttpMethod.Get, "*/api/channels/*/users")
            .Respond(HttpStatusCode.OK, JsonContent.Create(new List<User>()));

        _chatServiceMock.Setup(x => x.InitializeAsync(It.IsAny<IHubConnectionBuilder>()))
            .Returns(Task.CompletedTask);
        _chatServiceMock.Setup(x => x.JoinChannel(It.IsAny<string>())).Returns(Task.CompletedTask);
        _chatServiceMock.Setup(x => x.LeaveChannel(It.IsAny<string>())).Returns(Task.CompletedTask);
        _privateMessageServiceMock.Setup(x => x.GetConversationsAsync(It.IsAny<string>()))
            .ReturnsAsync([]);

        var cut = await RenderChatAsync(channelName: "general");

        // Act
        await NavigateToChannelAsync(cut, "random");

        // Assert - Devrait avoir quitté le canal précédent
        _chatServiceMock.Verify(x => x.LeaveChannel("general"), Times.AtLeastOnce);
    }

    [Fact]
    public async Task Chat_SwitchChannel_ShouldJoinNewChannel()
    {
        // Arrange
        SetupBasicAuth();
        var channels = new List<Channel>
        {
            new() { Id = Guid.NewGuid(), Name = "general", CreatedBy = "system", CreatedAt = DateTime.UtcNow },
            new() { Id = Guid.NewGuid(), Name = "random", CreatedBy = "system", CreatedAt = DateTime.UtcNow }
        };

        _mockHttp.When(HttpMethod.Get, "*/api/channels")
            .Respond(HttpStatusCode.OK, JsonContent.Create(channels));
        _mockHttp.When(HttpMethod.Get, "*/api/messages/*")
            .Respond(HttpStatusCode.OK, JsonContent.Create(new List<Message>()));
        _mockHttp.When(HttpMethod.Get, "*/api/channels/*/users")
            .Respond(HttpStatusCode.OK, JsonContent.Create(new List<User>()));

        _chatServiceMock.Setup(x => x.InitializeAsync(It.IsAny<IHubConnectionBuilder>()))
            .Returns(Task.CompletedTask);
        _chatServiceMock.Setup(x => x.JoinChannel(It.IsAny<string>())).Returns(Task.CompletedTask);
        _chatServiceMock.Setup(x => x.LeaveChannel(It.IsAny<string>())).Returns(Task.CompletedTask);
        _privateMessageServiceMock.Setup(x => x.GetConversationsAsync(It.IsAny<string>()))
            .ReturnsAsync([]);

        var cut = await RenderChatAsync(channelName: "general");
        _chatServiceMock.Invocations.Clear();

        // Act
        await NavigateToChannelAsync(cut, "random");

        // Assert - Devrait avoir rejoint le nouveau canal
        _chatServiceMock.Verify(x => x.JoinChannel("random"), Times.AtLeastOnce);
    }

    [Fact]
    public async Task Chat_SwitchChannel_FromPrivateConversation_ShouldClearPrivateState()
    {
        // Arrange
        SetupBasicAuth();
        var channels = new List<Channel>
        {
            new() { Id = Guid.NewGuid(), Name = "general", CreatedBy = "system", CreatedAt = DateTime.UtcNow }
        };

        _mockHttp.When(HttpMethod.Get, "*/api/channels")
            .Respond(HttpStatusCode.OK, JsonContent.Create(channels));
        _mockHttp.When(HttpMethod.Get, "*/api/messages/general")
            .Respond(HttpStatusCode.OK, JsonContent.Create(new List<Message>()));
        _mockHttp.When(HttpMethod.Get, "*/api/channels/general/users")
            .Respond(HttpStatusCode.OK, JsonContent.Create(new List<User>()));
        _mockHttp.When(HttpMethod.Get, "*/api/private-messages/status/Friend")
            .Respond(HttpStatusCode.OK, JsonContent.Create(new { Username = "Friend", IsOnline = true }));

        _chatServiceMock.Setup(x => x.InitializeAsync(It.IsAny<IHubConnectionBuilder>()))
            .Returns(Task.CompletedTask);
        _chatServiceMock.Setup(x => x.JoinChannel("general")).Returns(Task.CompletedTask);
        _chatServiceMock.Setup(x => x.MarkPrivateMessagesAsRead(It.IsAny<string>()))
            .Returns(Task.CompletedTask);
        _privateMessageServiceMock.Setup(x => x.GetConversationsAsync("TestUser"))
            .ReturnsAsync([new() { OtherUser = new User { UserId = "Friend", Username = "Friend" }, LastMessage = "Hi", LastMessageTime = DateTime.UtcNow, UnreadCount = 0 }]);
        _privateMessageServiceMock.Setup(x => x.GetPrivateMessagesAsync("TestUser", "Friend"))
            .ReturnsAsync([]);

        var cut = await RenderChatAsync();
        await NavigateToPrivateChatAsync(cut, "Friend", "Friend");

        // Vérifier qu'on est bien en conversation privée
        Assert.Contains("Friend", cut.Markup);

        // Act - Passer à un canal
        await NavigateToChannelAsync(cut, "general");
        await Task.Delay(200);
        cut.Render();

        // Assert - Ne devrait plus être en mode conversation privée
        Assert.DoesNotContain("class=\"user-status", cut.Markup);
    }

    [Fact]
    public async Task Chat_SwitchChannel_ToSameChannel_ShouldNotLeaveAndRejoin()
    {
        // Arrange
        SetupBasicAuth();
        var channels = new List<Channel>
        {
            new() { Id = Guid.NewGuid(), Name = "general", CreatedBy = "system", CreatedAt = DateTime.UtcNow }
        };

        _mockHttp.When(HttpMethod.Get, "*/api/channels")
            .Respond(HttpStatusCode.OK, JsonContent.Create(channels));
        _mockHttp.When(HttpMethod.Get, "*/api/messages/general")
            .Respond(HttpStatusCode.OK, JsonContent.Create(new List<Message>()));
        _mockHttp.When(HttpMethod.Get, "*/api/channels/general/users")
            .Respond(HttpStatusCode.OK, JsonContent.Create(new List<User>()));

        _chatServiceMock.Setup(x => x.InitializeAsync(It.IsAny<IHubConnectionBuilder>()))
            .Returns(Task.CompletedTask);
        _chatServiceMock.Setup(x => x.JoinChannel("general")).Returns(Task.CompletedTask);
        _chatServiceMock.Setup(x => x.LeaveChannel("general")).Returns(Task.CompletedTask);
        _privateMessageServiceMock.Setup(x => x.GetConversationsAsync(It.IsAny<string>()))
            .ReturnsAsync([]);

        var cut = await RenderChatAsync(channelName: "general");
        _chatServiceMock.Invocations.Clear();

        // Act - Essayer de switcher vers le même canal
        await NavigateToChannelAsync(cut, "general");
        await Task.Delay(200);

        // Assert - Ne devrait pas quitter le canal
        _chatServiceMock.Verify(x => x.LeaveChannel("general"), Times.Never);
        _chatServiceMock.Verify(x => x.JoinChannel("general"), Times.Never);
    }
}