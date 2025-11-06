// tests/IrcChat.Client.Tests/Services/PrivateMessageServiceTests.cs
using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using IrcChat.Client.Services;
using IrcChat.Shared.Models;
using RichardSzalay.MockHttp;
using Xunit;

namespace IrcChat.Client.Tests.Services;

public class PrivateMessageServiceTests
{
    private readonly MockHttpMessageHandler _mockHttp;
    private readonly HttpClient _httpClient;

    public PrivateMessageServiceTests()
    {
        _mockHttp = new MockHttpMessageHandler();
        _httpClient = _mockHttp.ToHttpClient();
        _httpClient.BaseAddress = new Uri("https://localhost:7000");
    }

    [Fact]
    public void NotifyPrivateMessageReceived_ShouldTriggerEvents()
    {
        // Arrange
        var service = new PrivateMessageService(_httpClient);
        PrivateMessage? receivedMessage = null;
        var unreadCountChanged = false;

        service.OnPrivateMessageReceived += (msg) => receivedMessage = msg;
        service.OnUnreadCountChanged += () => unreadCountChanged = true;

        var testMessage = new PrivateMessage
        {
            Id = Guid.NewGuid(),
            SenderUsername = "sender",
            RecipientUsername = "recipient",
            Content = "Test",
            Timestamp = DateTime.UtcNow
        };

        // Act
        service.NotifyPrivateMessageReceived(testMessage);

        // Assert
        receivedMessage.Should().NotBeNull();
        receivedMessage.Should().BeEquivalentTo(testMessage);
        unreadCountChanged.Should().BeTrue();
    }

    [Fact]
    public void NotifyPrivateMessageSent_ShouldTriggerEvent()
    {
        // Arrange
        var service = new PrivateMessageService(_httpClient);
        PrivateMessage? sentMessage = null;

        service.OnPrivateMessageSent += (msg) => sentMessage = msg;

        var testMessage = new PrivateMessage
        {
            Id = Guid.NewGuid(),
            SenderUsername = "sender",
            RecipientUsername = "recipient",
            Content = "Test",
            Timestamp = DateTime.UtcNow
        };

        // Act
        service.NotifyPrivateMessageSent(testMessage);

        // Assert
        sentMessage.Should().NotBeNull();
        sentMessage.Should().BeEquivalentTo(testMessage);
    }

    [Fact]
    public void NotifyMessagesRead_ShouldTriggerEvent()
    {
        // Arrange
        var service = new PrivateMessageService(_httpClient);
        string? readUsername = null;
        List<Guid>? readMessageIds = null;

        service.OnMessagesRead += (username, messageIds) =>
        {
            readUsername = username;
            readMessageIds = messageIds;
        };

        var messageIds = new List<Guid> { Guid.NewGuid(), Guid.NewGuid() };

        // Act
        service.NotifyMessagesRead("testUser", messageIds);

        // Assert
        readUsername.Should().Be("testUser");
        readMessageIds.Should().BeEquivalentTo(messageIds);
    }

    [Fact]
    public async Task GetConversationsAsync_ShouldReturnConversations()
    {
        // Arrange
        var conversations = new List<PrivateConversation>
        {
            new()
            {
                OtherUsername = "user1",
                LastMessage = "Hello",
                LastMessageTime = DateTime.UtcNow,
                UnreadCount = 2
            },
            new()
            {
                OtherUsername = "user2",
                LastMessage = "Hi",
                LastMessageTime = DateTime.UtcNow.AddHours(-1),
                UnreadCount = 0
            }
        };

        _mockHttp.When(HttpMethod.Get, "*/api/private-messages/conversations/testUser")
            .Respond(HttpStatusCode.OK, JsonContent.Create(conversations));

        var service = new PrivateMessageService(_httpClient);

        // Act
        var result = await service.GetConversationsAsync("testUser");

        // Assert
        result.Should().HaveCount(2);
        result.Should().BeEquivalentTo(conversations);
    }

    [Fact]
    public async Task GetConversationsAsync_OnError_ShouldReturnEmptyList()
    {
        // Arrange
        _mockHttp.When(HttpMethod.Get, "*/api/private-messages/conversations/testUser")
            .Respond(HttpStatusCode.InternalServerError);

        var service = new PrivateMessageService(_httpClient);

        // Act
        var result = await service.GetConversationsAsync("testUser");

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetPrivateMessagesAsync_ShouldReturnMessages()
    {
        // Arrange
        var messages = new List<PrivateMessage>
        {
            new()
            {
                Id = Guid.NewGuid(),
                SenderUsername = "user1",
                RecipientUsername = "user2",
                Content = "Message 1",
                Timestamp = DateTime.UtcNow,
                IsRead = true
            },
            new()
            {
                Id = Guid.NewGuid(),
                SenderUsername = "user2",
                RecipientUsername = "user1",
                Content = "Message 2",
                Timestamp = DateTime.UtcNow,
                IsRead = false
            }
        };

        _mockHttp.When(HttpMethod.Get, "*/api/private-messages/user1/with/user2")
            .Respond(HttpStatusCode.OK, JsonContent.Create(messages));

        var service = new PrivateMessageService(_httpClient);

        // Act
        var result = await service.GetPrivateMessagesAsync("user1", "user2");

        // Assert
        result.Should().HaveCount(2);
        result.Should().BeEquivalentTo(messages);
    }

    [Fact]
    public async Task GetPrivateMessagesAsync_OnError_ShouldReturnEmptyList()
    {
        // Arrange
        _mockHttp.When(HttpMethod.Get, "*/api/private-messages/user1/with/user2")
            .Respond(HttpStatusCode.NotFound);

        var service = new PrivateMessageService(_httpClient);

        // Act
        var result = await service.GetPrivateMessagesAsync("user1", "user2");

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetUnreadCountAsync_ShouldReturnCount()
    {
        // Arrange
        var unreadResponse = new { UnreadCount = 5 };

        _mockHttp.When(HttpMethod.Get, "*/api/private-messages/testUser/unread-count")
            .Respond(HttpStatusCode.OK, JsonContent.Create(unreadResponse));

        var service = new PrivateMessageService(_httpClient);

        // Act
        var result = await service.GetUnreadCountAsync("testUser");

        // Assert
        result.Should().Be(5);
    }

    [Fact]
    public async Task GetUnreadCountAsync_OnError_ShouldReturnZero()
    {
        // Arrange
        _mockHttp.When(HttpMethod.Get, "*/api/private-messages/testUser/unread-count")
            .Respond(HttpStatusCode.InternalServerError);

        var service = new PrivateMessageService(_httpClient);

        // Act
        var result = await service.GetUnreadCountAsync("testUser");

        // Assert
        result.Should().Be(0);
    }

    [Fact]
    public async Task DeleteConversationAsync_ShouldReturnTrueAndTriggerEvent()
    {
        // Arrange
        _mockHttp.When(HttpMethod.Delete, "*/api/private-messages/user1/conversation/user2")
            .Respond(HttpStatusCode.OK);

        var service = new PrivateMessageService(_httpClient);
        string? deletedUsername = null;
        var unreadCountChanged = false;

        service.OnConversationDeleted += (username) => deletedUsername = username;
        service.OnUnreadCountChanged += () => unreadCountChanged = true;

        // Act
        var result = await service.DeleteConversationAsync("user1", "user2");

        // Assert
        result.Should().BeTrue();
        deletedUsername.Should().Be("user2");
        unreadCountChanged.Should().BeTrue();
    }

    [Fact]
    public async Task DeleteConversationAsync_OnError_ShouldReturnFalse()
    {
        // Arrange
        _mockHttp.When(HttpMethod.Delete, "*/api/private-messages/user1/conversation/user2")
            .Respond(HttpStatusCode.NotFound);

        var service = new PrivateMessageService(_httpClient);

        // Act
        var result = await service.DeleteConversationAsync("user1", "user2");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task DeleteConversationAsync_OnException_ShouldReturnFalse()
    {
        // Arrange
        _mockHttp.When(HttpMethod.Delete, "*/api/private-messages/user1/conversation/user2")
            .Throw(new HttpRequestException("Network error"));

        var service = new PrivateMessageService(_httpClient);

        // Act
        var result = await service.DeleteConversationAsync("user1", "user2");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void MultipleEventSubscribers_ShouldAllBeNotified()
    {
        // Arrange
        var service = new PrivateMessageService(_httpClient);
        var subscriber1Called = 0;
        var subscriber2Called = 0;

        service.OnPrivateMessageReceived += _ => subscriber1Called++;
        service.OnPrivateMessageReceived += _ => subscriber2Called++;

        var testMessage = new PrivateMessage
        {
            Id = Guid.NewGuid(),
            SenderUsername = "sender",
            RecipientUsername = "recipient",
            Content = "Test",
            Timestamp = DateTime.UtcNow
        };

        // Act
        service.NotifyPrivateMessageReceived(testMessage);

        // Assert
        subscriber1Called.Should().Be(1);
        subscriber2Called.Should().Be(1);
    }
}