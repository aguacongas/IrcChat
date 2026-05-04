// tests/IrcChat.Client.Tests/Components/MessageListTests.Reactions.cs
using IrcChat.Client.Components;
using IrcChat.Shared.Models;
using Microsoft.AspNetCore.Components;

namespace IrcChat.Client.Tests.Components;

public partial class MessageListTests
{
    // ===== ShowReactions — affichage conditionnel =====

    [Fact]
    public void MessageList_WhenShowReactionsTrue_ShouldRenderAddReactionButton()
    {
        // Arrange
        var messages = new List<IMessage>
        {
            new Message
            {
                Id = Guid.NewGuid(),
                Username = "user1",
                Content = "Hello!",
                Timestamp = DateTime.UtcNow,
            },
        };

        // Act
        var cut = Render<MessageList>(parameters => parameters
            .Add(p => p.Messages, messages)
            .Add(p => p.CurrentUsername, "testuser")
            .Add(p => p.ShowReactions, true));

        // Assert — le bouton 😊 est présent dans .message-actions
        Assert.Contains("add-reaction-btn", cut.Markup);
    }

    [Fact]
    public void MessageList_WhenShowReactionsFalse_ShouldNotRenderAddReactionButton()
    {
        // Arrange
        var messages = new List<IMessage>
        {
            new Message
            {
                Id = Guid.NewGuid(),
                Username = "user1",
                Content = "Hello!",
                Timestamp = DateTime.UtcNow,
            },
        };

        // Act
        var cut = Render<MessageList>(parameters => parameters
            .Add(p => p.Messages, messages)
            .Add(p => p.CurrentUsername, "testuser")
            .Add(p => p.ShowReactions, false));

        // Assert — pas de bouton de réaction
        Assert.Throws<ElementNotFoundException>(() => cut.Find(".add-reaction-btn"));
    }

    [Fact]
    public void MessageList_WhenShowReactionsDefault_ShouldNotRenderAddReactionButton()
    {
        // Arrange — ShowReactions non spécifié = false par défaut
        var messages = new List<IMessage>
        {
            new Message
            {
                Id = Guid.NewGuid(),
                Username = "user1",
                Content = "Hello!",
                Timestamp = DateTime.UtcNow,
            },
        };

        // Act
        var cut = Render<MessageList>(parameters => parameters
            .Add(p => p.Messages, messages)
            .Add(p => p.CurrentUsername, "testuser"));

        // Assert
        Assert.Throws<ElementNotFoundException>(() => cut.Find(".add-reaction-btn"));
    }

    // ===== Affichage des badges de réactions existantes =====

    [Fact]
    public void MessageList_WithReactions_ShouldRenderReactionBadges()
    {
        // Arrange
        var messages = new List<IMessage>
        {
            new Message
            {
                Id = Guid.NewGuid(),
                Username = "user1",
                Content = "Hello!",
                Timestamp = DateTime.UtcNow,
                Reactions =
                [
                    new MessageReactionDto { Emoji = "👍", Count = 3, UserIds = ["u1", "u2", "u3"], Usernames = ["a", "b", "c"] },
                    new MessageReactionDto { Emoji = "❤️", Count = 1, UserIds = ["u4"], Usernames = ["d"] },
                ],
            },
        };

        // Act
        var cut = Render<MessageList>(parameters => parameters
            .Add(p => p.Messages, messages)
            .Add(p => p.CurrentUsername, "testuser")
            .Add(p => p.ShowReactions, true));

        // Assert — deux badges dans .reactions-list
        var badges = cut.FindAll(".reaction-badge");
        Assert.Equal(2, badges.Count);
    }

    [Fact]
    public void MessageList_WithNoReactions_ShouldNotRenderReactionBadges()
    {
        // Arrange
        var messages = new List<IMessage>
        {
            new Message
            {
                Id = Guid.NewGuid(),
                Username = "user1",
                Content = "Hello!",
                Timestamp = DateTime.UtcNow,
                Reactions = [],
            },
        };

        // Act
        var cut = Render<MessageList>(parameters => parameters
            .Add(p => p.Messages, messages)
            .Add(p => p.CurrentUsername, "testuser")
            .Add(p => p.ShowReactions, true));

        // Assert — pas de badge, mais le bouton 😊 est quand même là
        Assert.Throws<ElementNotFoundException>(() => cut.Find(".reaction-badge"));
        Assert.Contains("add-reaction-btn", cut.Markup);
    }

    [Fact]
    public void MessageList_ReactionBadge_ShouldShowEmojiAndCount()
    {
        // Arrange
        var messages = new List<IMessage>
        {
            new Message
            {
                Id = Guid.NewGuid(),
                Username = "user1",
                Content = "Hello!",
                Timestamp = DateTime.UtcNow,
                Reactions =
                [
                    new MessageReactionDto { Emoji = "👍", Count = 5, UserIds = ["u1"], Usernames = ["alice"] },
                ],
            },
        };

        // Act
        var cut = Render<MessageList>(parameters => parameters
            .Add(p => p.Messages, messages)
            .Add(p => p.CurrentUsername, "testuser")
            .Add(p => p.ShowReactions, true));

        // Assert
        var badge = cut.Find(".reaction-badge");
        Assert.Contains("👍", badge.InnerHtml);
        Assert.Contains("5", badge.InnerHtml);
    }

    [Fact]
    public void MessageList_ReactionBadgeInSameLineAsDeleteButton()
    {
        // Arrange — vérifier que les badges sont dans .reactions-list
        // et le bouton 😊 est dans .message-actions (même zone que supprimer)
        var messages = new List<IMessage>
        {
            new Message
            {
                Id = Guid.NewGuid(),
                Username = "user1",
                Content = "Hello!",
                Timestamp = DateTime.UtcNow,
                Reactions =
                [
                    new MessageReactionDto { Emoji = "👍", Count = 1, UserIds = ["u1"], Usernames = ["alice"] },
                ],
            },
        };

        // Act
        var cut = Render<MessageList>(parameters => parameters
            .Add(p => p.Messages, messages)
            .Add(p => p.CurrentUsername, "testuser")
            .Add(p => p.ShowReactions, true)
            .Add(p => p.CanManage, true));

        // Assert — le bouton 😊 et 🗑️ sont tous les deux dans .message-actions
        var messageActions = cut.Find(".message-actions");
        Assert.NotNull(messageActions);
        Assert.Contains("add-reaction-btn", messageActions.InnerHtml);
        Assert.Contains("btn-delete-message", messageActions.InnerHtml);

        // Les badges de réactions sont dans .reactions-list (séparé de .message-actions)
        var reactionsList = cut.Find(".reactions-list");
        Assert.NotNull(reactionsList);
        Assert.Contains("👍", reactionsList.InnerHtml);
    }

    [Fact]
    public void MessageList_WithReactionsOnly_MessageActionsShouldContainOnlyEmojiButton()
    {
        // Arrange — ShowReactions=true mais CanManage=false → seulement le bouton 😊
        var messages = new List<IMessage>
        {
            new Message
            {
                Id = Guid.NewGuid(),
                Username = "user1",
                Content = "Hello!",
                Timestamp = DateTime.UtcNow,
                Reactions = [],
            },
        };

        // Act
        var cut = Render<MessageList>(parameters => parameters
            .Add(p => p.Messages, messages)
            .Add(p => p.CurrentUsername, "testuser")
            .Add(p => p.ShowReactions, true)
            .Add(p => p.CanManage, false));

        // Assert — .message-actions contient 😊 mais pas 🗑️
        var messageActions = cut.Find(".message-actions");
        Assert.Contains("add-reaction-btn", messageActions.InnerHtml);
        Assert.Throws<ElementNotFoundException>(() => cut.Find(".btn-delete-message"));
    }

    [Fact]
    public void MessageList_WithDeleteOnly_MessageActionsShouldContainOnlyDeleteButton()
    {
        // Arrange — ShowReactions=false mais CanManage=true → seulement le bouton 🗑️
        var messages = new List<IMessage>
        {
            new Message
            {
                Id = Guid.NewGuid(),
                Username = "user1",
                Content = "Hello!",
                Timestamp = DateTime.UtcNow,
            },
        };

        // Act
        var cut = Render<MessageList>(parameters => parameters
            .Add(p => p.Messages, messages)
            .Add(p => p.CurrentUsername, "testuser")
            .Add(p => p.ShowReactions, false)
            .Add(p => p.CanManage, true));

        // Assert — .message-actions contient 🗑️ mais pas 😊
        var messageActions = cut.Find(".message-actions");
        Assert.Contains("btn-delete-message", messageActions.InnerHtml);
        Assert.Throws<ElementNotFoundException>(() => cut.Find(".add-reaction-btn"));
    }

    // ===== Callback OnReact via badge existant =====

    [Fact]
    public async Task MessageList_WhenExistingBadgeClicked_ShouldInvokeOnReact()
    {
        // Arrange
        (Guid, string)? receivedReaction = null;
        var messageId = Guid.NewGuid();
        var messages = new List<IMessage>
        {
            new Message
            {
                Id = messageId,
                Username = "user1",
                Content = "Hello!",
                Timestamp = DateTime.UtcNow,
                Reactions =
                [
                    new MessageReactionDto { Emoji = "😂", Count = 1, UserIds = ["other"], Usernames = ["bob"] },
                ],
            },
        };

        var cut = Render<MessageList>(parameters => parameters
            .Add(p => p.Messages, messages)
            .Add(p => p.CurrentUsername, "testuser")
            .Add(p => p.ShowReactions, true)
            .Add(p => p.OnReact, EventCallback.Factory.Create<(Guid, string)>(
                this, r => receivedReaction = r)));

        // Act
        var badge = cut.Find(".reaction-badge");
        await cut.InvokeAsync(() => badge.Click());

        // Assert
        Assert.NotNull(receivedReaction);
        Assert.Equal(messageId, receivedReaction!.Value.Item1);
        Assert.Equal("😂", receivedReaction.Value.Item2);
    }

    [Fact]
    public async Task MessageList_WhenQuickEmojiSelectedFromPicker_ShouldInvokeOnReact()
    {
        // Arrange
        (Guid, string)? receivedReaction = null;
        var messageId = Guid.NewGuid();
        var messages = new List<IMessage>
        {
            new Message
            {
                Id = messageId,
                Username = "user1",
                Content = "Hello!",
                Timestamp = DateTime.UtcNow,
                Reactions = [],
            },
        };

        var cut = Render<MessageList>(parameters => parameters
            .Add(p => p.Messages, messages)
            .Add(p => p.CurrentUsername, "testuser")
            .Add(p => p.ShowReactions, true)
            .Add(p => p.OnReact, EventCallback.Factory.Create<(Guid, string)>(
                this, r => receivedReaction = r)));

        // Ouvrir le picker
        var addBtn = cut.Find(".add-reaction-btn");
        await cut.InvokeAsync(() => addBtn.Click());

        // Act — cliquer sur le premier emoji rapide (👍)
        var quickBtn = cut.Find(".quick-emoji-btn");
        await cut.InvokeAsync(() => quickBtn.Click());

        // Assert
        Assert.NotNull(receivedReaction);
        Assert.Equal(messageId, receivedReaction!.Value.Item1);
        Assert.Equal("👍", receivedReaction.Value.Item2);
    }

    [Fact]
    public async Task MessageList_MultipleMessages_EachReactionInvokesCorrectMessageId()
    {
        // Arrange
        var capturedReactions = new List<(Guid, string)>();
        var messageId1 = Guid.NewGuid();
        var messageId2 = Guid.NewGuid();
        var messages = new List<IMessage>
        {
            new Message
            {
                Id = messageId1,
                Username = "user1",
                Content = "First!",
                Timestamp = DateTime.UtcNow,
                Reactions =
                [
                    new MessageReactionDto { Emoji = "👍", Count = 1, UserIds = ["u1"], Usernames = ["alice"] },
                ],
            },
            new Message
            {
                Id = messageId2,
                Username = "user2",
                Content = "Second!",
                Timestamp = DateTime.UtcNow,
                Reactions =
                [
                    new MessageReactionDto { Emoji = "❤️", Count = 1, UserIds = ["u2"], Usernames = ["bob"] },
                ],
            },
        };

        var cut = Render<MessageList>(parameters => parameters
            .Add(p => p.Messages, messages)
            .Add(p => p.CurrentUsername, "testuser")
            .Add(p => p.ShowReactions, true)
            .Add(p => p.OnReact, EventCallback.Factory.Create<(Guid, string)>(
                this, r => capturedReactions.Add(r))));

        // Act — cliquer sur les deux badges
        var badges = cut.FindAll(".reaction-badge");
        await cut.InvokeAsync(() => badges[0].Click());
        badges = cut.FindAll(".reaction-badge");
        await cut.InvokeAsync(() => badges[1].Click());

        // Assert
        Assert.Equal(2, capturedReactions.Count);
        Assert.Equal(messageId1, capturedReactions[0].Item1);
        Assert.Equal("👍", capturedReactions[0].Item2);
        Assert.Equal(messageId2, capturedReactions[1].Item1);
        Assert.Equal("❤️", capturedReactions[1].Item2);
    }

    // ===== CurrentUserId propagé — classe "own" =====

    [Fact]
    public void MessageList_ShouldMarkOwnReactionWithOwnClass()
    {
        // Arrange
        var currentUserId = "my-user-id";
        var messages = new List<IMessage>
        {
            new Message
            {
                Id = Guid.NewGuid(),
                Username = "user1",
                Content = "Hello!",
                Timestamp = DateTime.UtcNow,
                Reactions =
                [
                    new MessageReactionDto
                    {
                        Emoji = "👍",
                        Count = 1,
                        UserIds = [currentUserId],
                        Usernames = ["me"],
                    },
                ],
            },
        };

        // Act
        var cut = Render<MessageList>(parameters => parameters
            .Add(p => p.Messages, messages)
            .Add(p => p.CurrentUsername, "testuser")
            .Add(p => p.CurrentUserId, currentUserId)
            .Add(p => p.ShowReactions, true));

        // Assert
        var badge = cut.Find(".reaction-badge");
        Assert.Contains("own", badge.ClassList);
    }

    [Fact]
    public void MessageList_OtherUserReaction_ShouldNotHaveOwnClass()
    {
        // Arrange
        var messages = new List<IMessage>
        {
            new Message
            {
                Id = Guid.NewGuid(),
                Username = "user1",
                Content = "Hello!",
                Timestamp = DateTime.UtcNow,
                Reactions =
                [
                    new MessageReactionDto
                    {
                        Emoji = "👍",
                        Count = 1,
                        UserIds = ["other-user-id"],
                        Usernames = ["other"],
                    },
                ],
            },
        };

        // Act
        var cut = Render<MessageList>(parameters => parameters
            .Add(p => p.Messages, messages)
            .Add(p => p.CurrentUsername, "testuser")
            .Add(p => p.CurrentUserId, "my-user-id")
            .Add(p => p.ShowReactions, true));

        // Assert
        var badge = cut.Find(".reaction-badge");
        Assert.DoesNotContain("own", badge.ClassList);
    }

    // ===== Photos éphémères — pas de réactions =====

    [Fact]
    public void MessageList_EphemeralPhoto_ShouldNotShowReactionButton()
    {
        // Arrange — les photos éphémères n'ont pas de bouton de réaction
        var messages = new List<IMessage>
        {
            new EphemeralPhotoDto
            {
                Id = Guid.NewGuid(),
                SenderUsername = "user1",
                SenderId = "u1",
                ChannelId = "general",
                ImageUrl = "https://example.com/img.jpg",
                ThumbnailUrl = "https://example.com/thumb.jpg",
                Timestamp = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddSeconds(3),
            },
        };

        // Act
        var cut = Render<MessageList>(parameters => parameters
            .Add(p => p.Messages, messages)
            .Add(p => p.CurrentUsername, "testuser")
            .Add(p => p.ShowReactions, true));

        // Assert
        Assert.Throws<ElementNotFoundException>(() => cut.Find(".add-reaction-btn"));
        Assert.Throws<ElementNotFoundException>(() => cut.Find(".reaction-badge"));
    }

    // ===== Tooltip sur les badges =====

    [Fact]
    public void MessageList_ReactionBadge_ShouldShowUsernamesInTooltip()
    {
        // Arrange
        var messages = new List<IMessage>
        {
            new Message
            {
                Id = Guid.NewGuid(),
                Username = "user1",
                Content = "Hello!",
                Timestamp = DateTime.UtcNow,
                Reactions =
                [
                    new MessageReactionDto { Emoji = "👍", Count = 2, UserIds = ["u1", "u2"], Usernames = ["alice", "bob"] },
                ],
            },
        };

        // Act
        var cut = Render<MessageList>(parameters => parameters
            .Add(p => p.Messages, messages)
            .Add(p => p.CurrentUsername, "testuser")
            .Add(p => p.ShowReactions, true));

        // Assert
        var badge = cut.Find(".reaction-badge");
        var title = badge.GetAttribute("title");
        Assert.Contains("alice", title);
        Assert.Contains("bob", title);
    }
}