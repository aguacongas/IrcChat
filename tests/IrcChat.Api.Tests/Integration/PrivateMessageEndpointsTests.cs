// tests/IrcChat.Api.Tests/Integration/PrivateMessageEndpointsTests.cs
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Http.Json;
using IrcChat.Api.Data;
using IrcChat.Shared.Models;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace IrcChat.Api.Tests.Integration;

public class PrivateMessageEndpointsTests(ApiWebApplicationFactory factory) : IClassFixture<ApiWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task GetConversations_WithNoMessages_ShouldReturnEmptyList()
    {
        // Act
        SetConnectionId("testuser");
        var response = await _client.GetAsync("/api/private-messages/conversations/testuser");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var conversations = await response.Content.ReadFromJsonAsync<List<PrivateConversation>>();
        Assert.NotNull(conversations);
        Assert.Empty(conversations);
    }


    [Fact]
    public async Task GetConversations_WithMessages_ShouldReturnConversationsWithOnlineStatus()
    {
        // Arrange
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ChatDbContext>();

        db.PrivateMessages.RemoveRange(db.PrivateMessages);
        await db.SaveChangesAsync();

        var sender = "user1";
        var recipient = "user2";

        // Ajouter des messages
        var message1 = new PrivateMessage
        {
            Id = Guid.NewGuid(),
            SenderUserId = sender,
            SenderUsername = sender,
            RecipientUserId = recipient,
            RecipientUsername = recipient,
            Content = "Hello",
            Timestamp = DateTime.UtcNow.AddMinutes(-5),
            IsRead = false,
            IsDeletedBySender = false,
            IsDeletedByRecipient = false
        };

        var message2 = new PrivateMessage
        {
            Id = Guid.NewGuid(),
            SenderUserId = recipient,
            SenderUsername = recipient,
            RecipientUserId = sender,
            RecipientUsername = sender,
            Content = "Hi there",
            Timestamp = DateTime.UtcNow,
            IsRead = false,
            IsDeletedBySender = false,
            IsDeletedByRecipient = false
        };

        db.PrivateMessages.AddRange(message1, message2);

        // Marquer user2 comme connecté
        var connectedUser = new ConnectedUser
        {
            Id = Guid.NewGuid(),
            UserId = recipient,
            Username = recipient,
            ConnectionId = "conn-123",
            ConnectedAt = DateTime.UtcNow,
            LastActivity = DateTime.UtcNow,
            ServerInstanceId = "server-1"
        };

        db.ConnectedUsers.Add(connectedUser);
        await db.SaveChangesAsync();

        // Act
        SetConnectionId(sender);
        var response = await _client.GetAsync($"/api/private-messages/conversations/{sender}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var verifyScope = factory.Services.CreateScope();
        using var verifyContext = verifyScope.ServiceProvider.GetRequiredService<ChatDbContext>();

        var conversations = await response.Content.ReadFromJsonAsync<List<PrivateConversation>>();
        Assert.NotNull(conversations);
        Assert.Single(conversations);

        var conversation = conversations[0];
        Assert.Equal(recipient, conversation.OtherUser?.UserId);
        Assert.Equal("Hi there", conversation.LastMessage);
        Assert.Equal(1, conversation.UnreadCount);
        Assert.True(conversation.IsOnline); // user2 est connecté
    }

    [Fact]
    public async Task GetConversations_WithOfflineUser_ShouldReturnOfflineStatus()
    {
        // Arrange
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ChatDbContext>();

        db.PrivateMessages.RemoveRange(db.PrivateMessages);
        await db.SaveChangesAsync();

        var sender = "user1";
        var recipient = "user3";

        var message = new PrivateMessage
        {
            Id = Guid.NewGuid(),
            SenderUserId = sender,
            SenderUsername = sender,
            RecipientUserId = recipient,
            RecipientUsername = recipient,
            Content = "Test",
            Timestamp = DateTime.UtcNow,
            IsRead = false,
            IsDeletedBySender = false,
            IsDeletedByRecipient = false
        };

        db.PrivateMessages.Add(message);
        await db.SaveChangesAsync();

        // Act
        SetConnectionId(sender);
        var response = await _client.GetAsync($"/api/private-messages/conversations/{sender}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var conversations = await response.Content.ReadFromJsonAsync<List<PrivateConversation>>();
        Assert.NotNull(conversations);
        Assert.Single(conversations);

        var conversation = conversations[0];
        Assert.Equal(recipient, conversation.OtherUser?.UserId);
        Assert.False(conversation.IsOnline); // user3 n'est pas connecté
    }

    [Fact]
    public async Task GetUserStatus_WithOnlineUser_ShouldReturnTrue()
    {
        // Arrange
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ChatDbContext>();

        var username = "onlineuser";
        var connectedUser = new ConnectedUser
        {
            Id = Guid.NewGuid(),
            UserId = username,
            Username = username,
            ConnectionId = "conn-456",
            ConnectedAt = DateTime.UtcNow,
            LastActivity = DateTime.UtcNow,
            ServerInstanceId = "server-1"
        };

        db.ConnectedUsers.Add(connectedUser);
        await db.SaveChangesAsync();

        // Act
        SetConnectionId(username);
        var response = await _client.GetAsync($"/api/private-messages/status/{username}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<UserStatusResponse>();
        Assert.NotNull(result);
        Assert.Equal(username, result.Username);
        Assert.True(result.IsOnline);
    }

    [Fact]
    public async Task GetUserStatus_WithOfflineUser_ShouldReturnFalse()
    {
        // Arrange
        var username = "offlineuser";

        // Act
        var response = await _client.GetAsync($"/api/private-messages/status/{username}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<UserStatusResponse>();
        Assert.NotNull(result);
        Assert.Equal(username, result.Username);
        Assert.False(result.IsOnline);
    }

    [Fact]
    public async Task GetConversations_WithMultipleUsers_ShouldReturnCorrectOnlineStatuses()
    {
        // Arrange
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ChatDbContext>();

        var currentUser = "user1";
        var onlineUser = "user2";
        var offlineUser = "user3";

        // Messages avec user2 (online)
        db.PrivateMessages.Add(new PrivateMessage
        {
            Id = Guid.NewGuid(),
            SenderUserId = currentUser,
            SenderUsername = currentUser,
            RecipientUserId = onlineUser,
            RecipientUsername = onlineUser,
            Content = "Hello online",
            Timestamp = DateTime.UtcNow,
            IsRead = false,
            IsDeletedBySender = false,
            IsDeletedByRecipient = false
        });

        // Messages avec user3 (offline)
        db.PrivateMessages.Add(new PrivateMessage
        {
            Id = Guid.NewGuid(),
            SenderUserId = currentUser,
            SenderUsername = currentUser,
            RecipientUserId = offlineUser,
            RecipientUsername = offlineUser,
            Content = "Hello offline",
            Timestamp = DateTime.UtcNow,
            IsRead = false,
            IsDeletedBySender = false,
            IsDeletedByRecipient = false
        });

        // Marquer user2 comme connecté
        db.ConnectedUsers.Add(new ConnectedUser
        {
            Id = Guid.NewGuid(),
            UserId = onlineUser,
            Username = onlineUser,
            ConnectionId = "conn-789",
            ConnectedAt = DateTime.UtcNow,
            LastActivity = DateTime.UtcNow,
            ServerInstanceId = "server-1"
        });

        await db.SaveChangesAsync();

        // Act
        SetConnectionId(currentUser);
        var response = await _client.GetAsync($"/api/private-messages/conversations/{currentUser}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var conversations = await response.Content.ReadFromJsonAsync<List<PrivateConversation>>();
        Assert.NotNull(conversations);
        Assert.Equal(2, conversations.Count);

        var onlineConv = conversations.First(c => c.OtherUser?.UserId == onlineUser);
        var offlineConv = conversations.First(c => c.OtherUser?.UserId == offlineUser);

        Assert.True(onlineConv.IsOnline);
        Assert.False(offlineConv.IsOnline);
    }

    [Fact]
    public async Task GetPrivateMessages_ShouldReturnMessagesInCorrectOrder()
    {
        // Arrange
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ChatDbContext>();

        var user1 = "alice";
        var user2 = "bob";

        var msg1 = new PrivateMessage
        {
            Id = Guid.NewGuid(),
            SenderUserId = user1,
            SenderUsername = user1,
            RecipientUserId = user2,
            RecipientUsername = user2,
            Content = "First",
            Timestamp = DateTime.UtcNow.AddMinutes(-10),
            IsRead = true,
            IsDeletedBySender = false,
            IsDeletedByRecipient = false
        };

        var msg2 = new PrivateMessage
        {
            Id = Guid.NewGuid(),
            SenderUserId = user2,
            SenderUsername = user2,
            RecipientUserId = user1,
            RecipientUsername = user1,
            Content = "Second",
            Timestamp = DateTime.UtcNow.AddMinutes(-5),
            IsRead = true,
            IsDeletedBySender = false,
            IsDeletedByRecipient = false
        };

        var msg3 = new PrivateMessage
        {
            Id = Guid.NewGuid(),
            SenderUserId = user1,
            SenderUsername = user1,
            RecipientUserId = user2,
            RecipientUsername = user2,
            Content = "Third",
            Timestamp = DateTime.UtcNow,
            IsRead = false,
            IsDeletedBySender = false,
            IsDeletedByRecipient = false
        };

        db.PrivateMessages.AddRange(msg1, msg2, msg3);
        await db.SaveChangesAsync();

        // Act
        SetConnectionId(user1);
        var response = await _client.GetAsync($"/api/private-messages/{user1}/with/{user2}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var messages = await response.Content.ReadFromJsonAsync<List<PrivateMessage>>();
        Assert.NotNull(messages);
        Assert.Equal(3, messages.Count);
        Assert.Equal("First", messages[0].Content);
        Assert.Equal("Second", messages[1].Content);
        Assert.Equal("Third", messages[2].Content);
    }

    [Fact]
    public async Task GetUnreadCount_ShouldReturnCorrectCount()
    {
        // Arrange
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ChatDbContext>();

        var recipient = "testuser";

        // 2 messages non lus
        db.PrivateMessages.AddRange(
            new PrivateMessage
            {
                Id = Guid.NewGuid(),
                SenderUserId = "sender1",
                SenderUsername = "sender1",
                RecipientUserId = recipient,
                RecipientUsername = recipient,
                Content = "Unread 1",
                Timestamp = DateTime.UtcNow,
                IsRead = false,
                IsDeletedBySender = false,
                IsDeletedByRecipient = false
            },
            new PrivateMessage
            {
                Id = Guid.NewGuid(),
                SenderUserId = "sender2",
                SenderUsername = "sender2",
                RecipientUserId = recipient,
                RecipientUsername = recipient,
                Content = "Unread 2",
                Timestamp = DateTime.UtcNow,
                IsRead = false,
                IsDeletedBySender = false,
                IsDeletedByRecipient = false
            },
            // 1 message lu
            new PrivateMessage
            {
                Id = Guid.NewGuid(),
                SenderUserId = "sender3",
                SenderUsername = "sender3",
                RecipientUserId = recipient,
                RecipientUsername = recipient,
                Content = "Read",
                Timestamp = DateTime.UtcNow,
                IsRead = true,
                IsDeletedBySender = false,
                IsDeletedByRecipient = false
            }
        );

        await db.SaveChangesAsync();

        // Act
        SetConnectionId(recipient);
        var response = await _client.GetAsync($"/api/private-messages/{recipient}/unread-count");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<UnreadCountResponse>();
        Assert.NotNull(result);
        Assert.Equal(2, result.UnreadCount);
    }

    [Fact]
    public async Task DeleteConversation_ShouldMarkMessagesAsDeletedForUserOnly()
    {
        // Arrange
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ChatDbContext>();

        db.PrivateMessages.RemoveRange(db.PrivateMessages);
        await db.SaveChangesAsync();

        var user1 = "user1";
        var user2 = "user2";

        var msg1 = new PrivateMessage
        {
            Id = Guid.NewGuid(),
            SenderUserId = user1,
            SenderUsername = user1,
            RecipientUserId = user2,
            RecipientUsername = user2,
            Content = "Message 1",
            Timestamp = DateTime.UtcNow,
            IsRead = false,
            IsDeletedBySender = false,
            IsDeletedByRecipient = false
        };

        var msg2 = new PrivateMessage
        {
            Id = Guid.NewGuid(),
            SenderUserId = user2,
            SenderUsername = user2,
            RecipientUserId = user1,
            RecipientUsername = user1,
            Content = "Message 2",
            Timestamp = DateTime.UtcNow,
            IsRead = false,
            IsDeletedBySender = false,
            IsDeletedByRecipient = false
        };

        db.PrivateMessages.AddRange(msg1, msg2);
        await db.SaveChangesAsync();

        // Act - user1 supprime la conversation
        SetConnectionId(user1);
        var response = await _client.DeleteAsync($"/api/private-messages/{user1}/conversation/{user2}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<DeleteConversationResponse>();
        Assert.NotNull(result);
        Assert.Equal(2, result.Deleted);

        // Vérifier en BDD
        using var verifyScope = factory.Services.CreateScope();
        using var verifyContext = verifyScope.ServiceProvider.GetRequiredService<ChatDbContext>();

        var deletedMsg1 = await verifyContext.PrivateMessages.FindAsync(msg1.Id);
        var deletedMsg2 = await verifyContext.PrivateMessages.FindAsync(msg2.Id);

        Assert.NotNull(deletedMsg1);
        Assert.NotNull(deletedMsg2);

        // msg1: envoyé par user1 -> IsDeletedBySender = true
        Assert.True(deletedMsg1.IsDeletedBySender);
        Assert.False(deletedMsg1.IsDeletedByRecipient);

        // msg2: reçu par user1 -> IsDeletedByRecipient = true
        Assert.False(deletedMsg2.IsDeletedBySender);
        Assert.True(deletedMsg2.IsDeletedByRecipient);
    }

    [Fact]
    public async Task DeleteConversation_OnlyDeletesForRequestingUser()
    {
        // Arrange
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ChatDbContext>();

        db.PrivateMessages.RemoveRange(db.PrivateMessages);
        await db.SaveChangesAsync();

        var user1 = "user1";
        var user2 = "user2";

        var msg = new PrivateMessage
        {
            Id = Guid.NewGuid(),
            SenderUserId = user1,
            SenderUsername = user1,
            RecipientUserId = user2,
            RecipientUsername = user2,
            Content = "Test message",
            Timestamp = DateTime.UtcNow,
            IsRead = false,
            IsDeletedBySender = false,
            IsDeletedByRecipient = false
        };

        db.PrivateMessages.Add(msg);
        await db.SaveChangesAsync();

        // Act - user1 supprime la conversation
        SetConnectionId(user1);
        await _client.DeleteAsync($"/api/private-messages/{user1}/conversation/{user2}");

        // Assert - user1 ne voit plus la conversation
        var response1 = await _client.GetAsync($"/api/private-messages/conversations/{user1}");
        var conversations1 = await response1.Content.ReadFromJsonAsync<List<PrivateConversation>>();
        Assert.NotNull(conversations1);
        Assert.Empty(conversations1);

        // Assert - user2 voit toujours la conversation
        SetConnectionId(user2);
        var response2 = await _client.GetAsync($"/api/private-messages/conversations/{user2}");
        var conversations2 = await response2.Content.ReadFromJsonAsync<List<PrivateConversation>>();
        Assert.NotNull(conversations2);
        Assert.Single(conversations2);
    }

    [Fact]
    public async Task DeleteConversation_BothUsersCanDeleteIndependently()
    {
        // Arrange
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ChatDbContext>();

        db.PrivateMessages.RemoveRange(db.PrivateMessages);
        await db.SaveChangesAsync();

        var user1 = "user1";
        var user2 = "user2";

        var msg = new PrivateMessage
        {
            Id = Guid.NewGuid(),
            SenderUserId = user1,
            SenderUsername = user1,
            RecipientUserId = user2,
            RecipientUsername = user2,
            Content = "Test message",
            Timestamp = DateTime.UtcNow,
            IsRead = false,
            IsDeletedBySender = false,
            IsDeletedByRecipient = false
        };

        db.PrivateMessages.Add(msg);
        await db.SaveChangesAsync();

        // Act - user1 supprime sa vue
        SetConnectionId(user1);
        await _client.DeleteAsync($"/api/private-messages/{user1}/conversation/{user2}");

        // Act - user2 supprime sa vue
        SetConnectionId(user2);
        await _client.DeleteAsync($"/api/private-messages/{user2}/conversation/{user1}");

        // Assert - Les deux utilisateurs ne voient plus la conversation
        SetConnectionId(user1);
        var response1 = await _client.GetAsync($"/api/private-messages/conversations/{user1}");
        var conversations1 = await response1.Content.ReadFromJsonAsync<List<PrivateConversation>>();
        Assert.NotNull(conversations1);
        Assert.Empty(conversations1);

        SetConnectionId(user2);
        var response2 = await _client.GetAsync($"/api/private-messages/conversations/{user2}");
        var conversations2 = await response2.Content.ReadFromJsonAsync<List<PrivateConversation>>();
        Assert.NotNull(conversations2);
        Assert.Empty(conversations2);

        // Assert - Le message est marqué comme supprimé des deux côtés
        using var verifyScope = factory.Services.CreateScope();
        using var verifyContext = verifyScope.ServiceProvider.GetRequiredService<ChatDbContext>();
        var deletedMsg = await verifyContext.PrivateMessages.FindAsync(msg.Id);
        Assert.NotNull(deletedMsg);
        Assert.True(deletedMsg.IsDeletedBySender);
        Assert.True(deletedMsg.IsDeletedByRecipient);
    }

    [Fact]
    public async Task DeleteConversation_WithNoMessages_ShouldReturnNotFound()
    {
        // Act
        SetConnectionId("user1");
        var response = await _client.DeleteAsync("/api/private-messages/user1/conversation/user2");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetUnreadCount_ShouldExcludeDeletedMessages()
    {
        // Arrange
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ChatDbContext>();

        var recipient = "testuser";

        // Message non lu et non supprimé
        db.PrivateMessages.Add(new PrivateMessage
        {
            Id = Guid.NewGuid(),
            SenderUserId = "sender1",
            SenderUsername = "sender1",
            RecipientUserId = recipient,
            RecipientUsername = recipient,
            Content = "Unread",
            Timestamp = DateTime.UtcNow,
            IsRead = false,
            IsDeletedBySender = false,
            IsDeletedByRecipient = false
        });

        // Message non lu mais supprimé par le destinataire
        db.PrivateMessages.Add(new PrivateMessage
        {
            Id = Guid.NewGuid(),
            SenderUserId = "sender2",
            SenderUsername = "sender2",
            RecipientUserId = recipient,
            RecipientUsername = recipient,
            Content = "Unread but deleted",
            Timestamp = DateTime.UtcNow,
            IsRead = false,
            IsDeletedBySender = false,
            IsDeletedByRecipient = true
        });

        await db.SaveChangesAsync();

        // Act
        SetConnectionId(recipient);
        var response = await _client.GetAsync($"/api/private-messages/{recipient}/unread-count");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<UnreadCountResponse>();
        Assert.NotNull(result);
        Assert.Equal(1, result.UnreadCount); // Seulement le message non supprimé
    }
    private void SetConnectionId(string userId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ChatDbContext>();
        var connectionId = Guid.NewGuid().ToString();
        db.ConnectedUsers.Add(new ConnectedUser
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Username = userId,
            ConnectionId = connectionId,
            ConnectedAt = DateTime.UtcNow,
            LastActivity = DateTime.UtcNow,
            ServerInstanceId = "test-server"
        });
        db.SaveChanges();
        _client.DefaultRequestHeaders.Remove("X-ConnectionId");
        _client.DefaultRequestHeaders.Add("X-ConnectionId", connectionId);
    }

    private sealed class UserStatusResponse
    {
        public string Username { get; set; } = string.Empty;
        [SuppressMessage("Major Code Smell", "S1144:Unused private types or members should be removed", Justification = "Deserialized")]
        public bool IsOnline { get; set; }
    }

    private sealed class UnreadCountResponse
    {
        [SuppressMessage("Major Code Smell", "S1144:Unused private types or members should be removed", Justification = "Deserialized")]
        public int UnreadCount { get; set; }
    }

    private sealed class DeleteConversationResponse
    {
        [SuppressMessage("Major Code Smell", "S1144:Unused private types or members should be removed", Justification = "Deserialized")]
        public int Deleted { get; set; }
    }
}