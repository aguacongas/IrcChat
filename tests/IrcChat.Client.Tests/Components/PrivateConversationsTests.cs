// tests/IrcChat.Client.Tests/Components/PrivateConversationsTests.cs
using Bunit;
using IrcChat.Client.Components;
using IrcChat.Shared.Models;
using Microsoft.AspNetCore.Components;
using Xunit;

namespace IrcChat.Client.Tests.Components;

public class PrivateConversationsTests : TestContext
{
    [Fact]
    public void Component_WhenNoConversations_ShouldShowEmptyState()
    {
        // Arrange
        var conversations = new List<PrivateConversation>();

        // Act
        var cut = RenderComponent<PrivateConversations>(parameters => parameters
            .Add(p => p.Conversations, conversations));

        // Assert
        Assert.Contains("Aucune conversation", cut.Markup);
        Assert.Contains("Cliquez sur un utilisateur pour démarrer une conversation", cut.Markup);
    }

    [Fact]
    public void Component_WithConversations_ShouldDisplayThem()
    {
        // Arrange
        var conversations = new List<PrivateConversation>
        {
            new()
            {
                OtherUsername = "alice",
                LastMessage = "Hello",
                LastMessageTime = DateTime.UtcNow,
                UnreadCount = 0,
                IsOnline = true
            },
            new()
            {
                OtherUsername = "bob",
                LastMessage = "Hi there",
                LastMessageTime = DateTime.UtcNow.AddMinutes(-5),
                UnreadCount = 2,
                IsOnline = false
            }
        };

        // Act
        var cut = RenderComponent<PrivateConversations>(parameters => parameters
            .Add(p => p.Conversations, conversations));

        // Assert
        Assert.Contains("alice", cut.Markup);
        Assert.Contains("bob", cut.Markup);
        Assert.Contains("Hello", cut.Markup);
        Assert.Contains("Hi there", cut.Markup);
    }

    [Fact]
    public void Component_WithOnlineUser_ShouldShowOnlineIndicator()
    {
        // Arrange
        var conversations = new List<PrivateConversation>
        {
            new()
            {
                OtherUsername = "onlineuser",
                LastMessage = "Test",
                LastMessageTime = DateTime.UtcNow,
                UnreadCount = 0,
                IsOnline = true
            }
        };

        // Act
        var cut = RenderComponent<PrivateConversations>(parameters => parameters
            .Add(p => p.Conversations, conversations));

        // Assert
        var statusDot = cut.Find(".status-dot.online");
        Assert.NotNull(statusDot);
        Assert.Contains("En ligne", statusDot.GetAttribute("title"));
    }

    [Fact]
    public void Component_WithOfflineUser_ShouldShowOfflineIndicator()
    {
        // Arrange
        var conversations = new List<PrivateConversation>
        {
            new()
            {
                OtherUsername = "offlineuser",
                LastMessage = "Test",
                LastMessageTime = DateTime.UtcNow,
                UnreadCount = 0,
                IsOnline = false
            }
        };

        // Act
        var cut = RenderComponent<PrivateConversations>(parameters => parameters
            .Add(p => p.Conversations, conversations));

        // Assert
        var statusDot = cut.Find(".status-dot.offline");
        Assert.NotNull(statusDot);
        Assert.Contains("Hors ligne", statusDot.GetAttribute("title"));
    }

    [Fact]
    public void Component_WithUnreadMessages_ShouldShowUnreadCount()
    {
        // Arrange
        var conversations = new List<PrivateConversation>
        {
            new()
            {
                OtherUsername = "user1",
                LastMessage = "Test",
                LastMessageTime = DateTime.UtcNow,
                UnreadCount = 3,
                IsOnline = true
            }
        };

        // Act
        var cut = RenderComponent<PrivateConversations>(parameters => parameters
            .Add(p => p.Conversations, conversations));

        // Assert
        var unreadBadge = cut.Find(".unread-count");
        Assert.NotNull(unreadBadge);
        Assert.Contains("3", unreadBadge.TextContent);
    }

    [Fact]
    public void Component_WithMultipleUnreadConversations_ShouldShowTotalUnreadCount()
    {
        // Arrange
        var conversations = new List<PrivateConversation>
        {
            new()
            {
                OtherUsername = "user1",
                LastMessage = "Test1",
                LastMessageTime = DateTime.UtcNow,
                UnreadCount = 2,
                IsOnline = true
            },
            new()
            {
                OtherUsername = "user2",
                LastMessage = "Test2",
                LastMessageTime = DateTime.UtcNow,
                UnreadCount = 3,
                IsOnline = false
            }
        };

        // Act
        var cut = RenderComponent<PrivateConversations>(parameters => parameters
            .Add(p => p.Conversations, conversations));

        // Assert
        var totalUnreadBadge = cut.Find(".section-header .unread-badge");
        Assert.NotNull(totalUnreadBadge);
        Assert.Contains("5", totalUnreadBadge.TextContent);
    }

    [Fact]
    public async Task Component_WhenClickingConversation_ShouldInvokeCallback()
    {
        // Arrange
        var conversations = new List<PrivateConversation>
        {
            new()
            {
                OtherUsername = "alice",
                LastMessage = "Test",
                LastMessageTime = DateTime.UtcNow,
                UnreadCount = 0,
                IsOnline = true
            }
        };

        var selectedUsername = string.Empty;

        var cut = RenderComponent<PrivateConversations>(parameters => parameters
            .Add(p => p.Conversations, conversations)
            .Add(p => p.OnConversationSelected, EventCallback.Factory.Create<string>(
                this, username => selectedUsername = username)));

        cut.Render();

        // Act
        var conversationItem = cut.Find(".conversation-list li");
        await cut.InvokeAsync(() => conversationItem.Click());

        // Assert
        Assert.Equal("alice", selectedUsername);
    }

    [Fact]
    public async Task Component_WhenClickingDeleteButton_ShouldInvokeDeleteCallback()
    {
        // Arrange
        var conversations = new List<PrivateConversation>
        {
            new()
            {
                OtherUsername = "bob",
                LastMessage = "Test",
                LastMessageTime = DateTime.UtcNow,
                UnreadCount = 0,
                IsOnline = false
            }
        };

        var deletedUsername = string.Empty;

        var cut = RenderComponent<PrivateConversations>(parameters => parameters
            .Add(p => p.Conversations, conversations)
            .Add(p => p.OnConversationDeleted, EventCallback.Factory.Create<string>(
                this, username => deletedUsername = username)));

        cut.Render();

        // Act
        var deleteButton = cut.Find(".delete-conversation-btn");
        await cut.InvokeAsync(() => deleteButton.Click());

        // Assert
        Assert.Equal("bob", deletedUsername);
    }

    [Fact]
    public void Component_WithSelectedUser_ShouldHighlightConversation()
    {
        // Arrange
        var conversations = new List<PrivateConversation>
        {
            new()
            {
                OtherUsername = "alice",
                LastMessage = "Test",
                LastMessageTime = DateTime.UtcNow,
                UnreadCount = 0,
                IsOnline = true
            },
            new()
            {
                OtherUsername = "bob",
                LastMessage = "Test",
                LastMessageTime = DateTime.UtcNow,
                UnreadCount = 0,
                IsOnline = false
            }
        };

        // Act
        var cut = RenderComponent<PrivateConversations>(parameters => parameters
            .Add(p => p.Conversations, conversations)
            .Add(p => p.SelectedUser, "alice"));

        // Assert
        var items = cut.FindAll(".conversation-list li");
        Assert.Equal(2, items.Count);
        Assert.Contains("active", items[0].ClassName);
        Assert.DoesNotContain("active", items[1].ClassName);
    }

    [Fact]
    public void Component_WithLongMessage_ShouldTruncate()
    {
        // Arrange
        var longMessage = "This is a very long message that should be truncated when displayed in the list";
        var conversations = new List<PrivateConversation>
        {
            new()
            {
                OtherUsername = "user1",
                LastMessage = longMessage,
                LastMessageTime = DateTime.UtcNow,
                UnreadCount = 0,
                IsOnline = true
            }
        };

        // Act
        var cut = RenderComponent<PrivateConversations>(parameters => parameters
            .Add(p => p.Conversations, conversations));

        // Assert
        var lastMessageSpan = cut.Find(".last-message");
        Assert.NotNull(lastMessageSpan);
        Assert.Contains("...", lastMessageSpan.TextContent);
        Assert.True(lastMessageSpan.TextContent.Length < longMessage.Length);
    }

    [Fact]
    public void Component_WithRecentMessage_ShouldShowRelativeTime()
    {
        // Arrange
        var conversations = new List<PrivateConversation>
        {
            new()
            {
                OtherUsername = "user1",
                LastMessage = "Test",
                LastMessageTime = DateTime.UtcNow.AddMinutes(-30),
                UnreadCount = 0,
                IsOnline = true
            }
        };

        // Act
        var cut = RenderComponent<PrivateConversations>(parameters => parameters
            .Add(p => p.Conversations, conversations));

        // Assert
        var timeSpan = cut.Find(".message-time");
        Assert.NotNull(timeSpan);
        Assert.Contains("30min", timeSpan.TextContent);
    }
}