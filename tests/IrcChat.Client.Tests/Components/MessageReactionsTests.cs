// tests/IrcChat.Client.Tests/Components/MessageReactionsTests.cs
using System.Diagnostics.CodeAnalysis;
using IrcChat.Client.Components;
using IrcChat.Shared.Models;
using Microsoft.AspNetCore.Components;

namespace IrcChat.Client.Tests.Components;

public class MessageReactionsTests : BunitContext
{
    [SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "Constant")]
    private static readonly string CurrentUserId = "user-current";

    // ===== RENDU INITIAL =====

    [Fact]
    public void MessageReactions_WithNoReactions_ShouldNotRenderReactionList()
    {
        // Arrange & Act
        var cut = Render<MessageReactions>(parameters => parameters
            .Add(p => p.Reactions, [])
            .Add(p => p.CurrentUserId, CurrentUserId));

        // Assert
        Assert.Throws<ElementNotFoundException>(() => cut.Find(".reactions-list"));
    }

    [Fact]
    public void MessageReactions_WithReactions_ShouldRenderBadges()
    {
        // Arrange
        var reactions = new List<MessageReactionDto>
        {
            new() { Emoji = "👍", Count = 3, UserIds = ["u1", "u2", "u3"], Usernames = ["alice", "bob", "charlie"] },
            new() { Emoji = "❤️", Count = 1, UserIds = ["u4"], Usernames = ["diana"] },
        };

        // Act
        var cut = Render<MessageReactions>(parameters => parameters
            .Add(p => p.Reactions, reactions)
            .Add(p => p.CurrentUserId, CurrentUserId));

        // Assert
        var badges = cut.FindAll(".reaction-badge");
        Assert.Equal(2, badges.Count);
    }

    [Fact]
    public void MessageReactions_ShouldDisplayEmojiAndCount()
    {
        // Arrange
        var reactions = new List<MessageReactionDto>
        {
            new() { Emoji = "👍", Count = 5, UserIds = ["u1"], Usernames = ["alice"] },
        };

        // Act
        var cut = Render<MessageReactions>(parameters => parameters
            .Add(p => p.Reactions, reactions)
            .Add(p => p.CurrentUserId, CurrentUserId));

        // Assert
        var badge = cut.Find(".reaction-badge");
        Assert.Contains("👍", badge.InnerHtml);
        Assert.Contains("5", badge.InnerHtml);
    }

    [Fact]
    public void MessageReactions_WhenCurrentUserReacted_ShouldAddOwnClass()
    {
        // Arrange
        var reactions = new List<MessageReactionDto>
        {
            new() { Emoji = "👍", Count = 2, UserIds = [CurrentUserId, "u2"], Usernames = ["me", "bob"] },
        };

        // Act
        var cut = Render<MessageReactions>(parameters => parameters
            .Add(p => p.Reactions, reactions)
            .Add(p => p.CurrentUserId, CurrentUserId));

        // Assert
        var badge = cut.Find(".reaction-badge");
        Assert.Contains("own", badge.ClassList);
    }

    [Fact]
    public void MessageReactions_WhenCurrentUserDidNotReact_ShouldNotAddOwnClass()
    {
        // Arrange
        var reactions = new List<MessageReactionDto>
        {
            new() { Emoji = "👍", Count = 1, UserIds = ["other-user"], Usernames = ["bob"] },
        };

        // Act
        var cut = Render<MessageReactions>(parameters => parameters
            .Add(p => p.Reactions, reactions)
            .Add(p => p.CurrentUserId, CurrentUserId));

        // Assert
        var badge = cut.Find(".reaction-badge");
        Assert.DoesNotContain("own", badge.ClassList);
    }

    [Fact]
    public void MessageReactions_ShouldShowUsernamesInTooltip()
    {
        // Arrange
        var reactions = new List<MessageReactionDto>
        {
            new() { Emoji = "👍", Count = 2, UserIds = ["u1", "u2"], Usernames = ["alice", "bob"] },
        };

        // Act
        var cut = Render<MessageReactions>(parameters => parameters
            .Add(p => p.Reactions, reactions)
            .Add(p => p.CurrentUserId, CurrentUserId));

        // Assert
        var badge = cut.Find(".reaction-badge");
        var title = badge.GetAttribute("title");
        Assert.Contains("alice", title);
        Assert.Contains("bob", title);
    }

    // ===== BOUTON D'AJOUT =====

    [Fact]
    public void MessageReactions_ShouldAlwaysRenderAddReactionButton()
    {
        // Arrange & Act
        var cut = Render<MessageReactions>(parameters => parameters
            .Add(p => p.Reactions, [])
            .Add(p => p.CurrentUserId, CurrentUserId));

        // Assert
        var btn = cut.Find(".add-reaction-btn");
        Assert.NotNull(btn);
        Assert.Contains("😊", btn.TextContent);
    }

    [Fact]
    public void MessageReactions_Initially_PickerShouldBeHidden()
    {
        // Arrange & Act
        var cut = Render<MessageReactions>(parameters => parameters
            .Add(p => p.Reactions, [])
            .Add(p => p.CurrentUserId, CurrentUserId));

        // Assert
        Assert.Throws<ElementNotFoundException>(() => cut.Find(".emoji-picker"));
    }

    // ===== OUVERTURE/FERMETURE DU PICKER =====

    [Fact]
    public async Task MessageReactions_WhenAddButtonClicked_ShouldShowPicker()
    {
        // Arrange
        var cut = Render<MessageReactions>(parameters => parameters
            .Add(p => p.Reactions, [])
            .Add(p => p.CurrentUserId, CurrentUserId));

        // Act
        var btn = cut.Find(".add-reaction-btn");
        await cut.InvokeAsync(() => btn.Click());

        // Assert
        Assert.NotNull(cut.Find(".emoji-picker"));
    }

    [Fact]
    public async Task MessageReactions_WhenAddButtonClickedTwice_ShouldHidePicker()
    {
        // Arrange
        var cut = Render<MessageReactions>(parameters => parameters
            .Add(p => p.Reactions, [])
            .Add(p => p.CurrentUserId, CurrentUserId));

        var btn = cut.Find(".add-reaction-btn");

        // Act — ouvrir puis fermer
        await cut.InvokeAsync(() => btn.Click());
        await cut.InvokeAsync(() => btn.Click());

        // Assert
        Assert.Throws<ElementNotFoundException>(() => cut.Find(".emoji-picker"));
    }

    [Fact]
    public async Task MessageReactions_WhenPickerOpen_ShouldShowActiveClassOnButton()
    {
        // Arrange
        var cut = Render<MessageReactions>(parameters => parameters
            .Add(p => p.Reactions, [])
            .Add(p => p.CurrentUserId, CurrentUserId));

        // Act
        var btn = cut.Find(".add-reaction-btn");
        await cut.InvokeAsync(() => btn.Click());

        // Assert
        var updatedBtn = cut.Find(".add-reaction-btn");
        Assert.Contains("active", updatedBtn.ClassList);
    }

    // ===== QUICK EMOJIS =====

    [Fact]
    public async Task MessageReactions_WhenPickerOpen_ShouldShowSixQuickEmojis()
    {
        // Arrange
        var cut = Render<MessageReactions>(parameters => parameters
            .Add(p => p.Reactions, [])
            .Add(p => p.CurrentUserId, CurrentUserId));

        await cut.InvokeAsync(() => cut.Find(".add-reaction-btn").Click());

        // Assert
        var quickBtns = cut.FindAll(".quick-emoji-btn");
        Assert.Equal(6, quickBtns.Count);
    }

    [Fact]
    public async Task MessageReactions_WhenPickerOpen_ShouldShowCorrectQuickEmojis()
    {
        // Arrange
        var cut = Render<MessageReactions>(parameters => parameters
            .Add(p => p.Reactions, [])
            .Add(p => p.CurrentUserId, CurrentUserId));

        await cut.InvokeAsync(() => cut.Find(".add-reaction-btn").Click());

        // Assert — les 6 emojis rapides attendus
        var markup = cut.Find(".quick-emojis").InnerHtml;
        Assert.Contains("👍", markup);
        Assert.Contains("❤️", markup);
        Assert.Contains("😂", markup);
        Assert.Contains("😮", markup);
        Assert.Contains("😢", markup);
        Assert.Contains("😡", markup);
    }

    [Fact]
    public async Task MessageReactions_WhenQuickEmojiClicked_ShouldInvokeOnReact()
    {
        // Arrange
        string? selectedEmoji = null;
        var cut = Render<MessageReactions>(parameters => parameters
            .Add(p => p.Reactions, [])
            .Add(p => p.CurrentUserId, CurrentUserId)
            .Add(p => p.OnReact, EventCallback.Factory.Create<string>(this, e => selectedEmoji = e)));

        await cut.InvokeAsync(() => cut.Find(".add-reaction-btn").Click());

        // Act — cliquer sur le premier emoji rapide (👍)
        var firstQuickBtn = cut.Find(".quick-emoji-btn");
        await cut.InvokeAsync(() => firstQuickBtn.Click());

        // Assert
        Assert.NotNull(selectedEmoji);
        Assert.Equal("👍", selectedEmoji);
    }

    [Fact]
    public async Task MessageReactions_WhenQuickEmojiClicked_ShouldClosePicker()
    {
        // Arrange
        var cut = Render<MessageReactions>(parameters => parameters
            .Add(p => p.Reactions, [])
            .Add(p => p.CurrentUserId, CurrentUserId)
            .Add(p => p.OnReact, EventCallback.Factory.Create<string>(this, _ => { })));

        await cut.InvokeAsync(() => cut.Find(".add-reaction-btn").Click());

        // Act
        await cut.InvokeAsync(() => cut.Find(".quick-emoji-btn").Click());

        // Assert — picker fermé
        Assert.Throws<ElementNotFoundException>(() => cut.Find(".emoji-picker"));
    }

    // ===== PICKER COMPLET =====

    [Fact]
    public async Task MessageReactions_WhenPickerOpen_ShouldShowCategoryButtons()
    {
        // Arrange
        var cut = Render<MessageReactions>(parameters => parameters
            .Add(p => p.Reactions, [])
            .Add(p => p.CurrentUserId, CurrentUserId));

        await cut.InvokeAsync(() => cut.Find(".add-reaction-btn").Click());

        // Assert — au moins une catégorie
        var categories = cut.FindAll(".category-btn");
        Assert.True(categories.Count >= 1);
    }

    [Fact]
    public async Task MessageReactions_WhenPickerOpen_ShouldShowEmojiGrid()
    {
        // Arrange
        var cut = Render<MessageReactions>(parameters => parameters
            .Add(p => p.Reactions, [])
            .Add(p => p.CurrentUserId, CurrentUserId));

        await cut.InvokeAsync(() => cut.Find(".add-reaction-btn").Click());

        // Assert — grille d'emojis visible
        var emojiGrid = cut.Find(".picker-emojis");
        Assert.NotNull(emojiGrid);
        var emojiBtns = cut.FindAll(".full-emoji-btn");
        Assert.True(emojiBtns.Count > 0);
    }

    [Fact]
    public async Task MessageReactions_WhenCategoryChanged_ShouldShowActiveClass()
    {
        // Arrange
        var cut = Render<MessageReactions>(parameters => parameters
            .Add(p => p.Reactions, [])
            .Add(p => p.CurrentUserId, CurrentUserId));

        await cut.InvokeAsync(() => cut.Find(".add-reaction-btn").Click());

        // Act — cliquer sur la deuxième catégorie
        var categoryBtns = cut.FindAll(".category-btn");
        if (categoryBtns.Count >= 2)
        {
            await cut.InvokeAsync(() => categoryBtns[1].Click());

            // Assert — la 2e catégorie est active
            var updatedBtns = cut.FindAll(".category-btn");
            Assert.Contains("active", updatedBtns[1].ClassList);
            Assert.DoesNotContain("active", updatedBtns[0].ClassList);
        }
    }

    [Fact]
    public async Task MessageReactions_WhenFullEmojiClicked_ShouldInvokeOnReact()
    {
        // Arrange
        string? selectedEmoji = null;
        var cut = Render<MessageReactions>(parameters => parameters
            .Add(p => p.Reactions, [])
            .Add(p => p.CurrentUserId, CurrentUserId)
            .Add(p => p.OnReact, EventCallback.Factory.Create<string>(this, e => selectedEmoji = e)));

        await cut.InvokeAsync(() => cut.Find(".add-reaction-btn").Click());

        // Act — cliquer sur le premier emoji du picker complet
        var firstFullBtn = cut.Find(".full-emoji-btn");
        await cut.InvokeAsync(() => firstFullBtn.Click());

        // Assert
        Assert.NotNull(selectedEmoji);
        Assert.False(string.IsNullOrEmpty(selectedEmoji));
    }

    [Fact]
    public async Task MessageReactions_WhenFullEmojiClicked_ShouldClosePicker()
    {
        // Arrange
        var cut = Render<MessageReactions>(parameters => parameters
            .Add(p => p.Reactions, [])
            .Add(p => p.CurrentUserId, CurrentUserId)
            .Add(p => p.OnReact, EventCallback.Factory.Create<string>(this, _ => { })));

        await cut.InvokeAsync(() => cut.Find(".add-reaction-btn").Click());

        // Act
        await cut.InvokeAsync(() => cut.Find(".full-emoji-btn").Click());

        // Assert — picker fermé
        Assert.Throws<ElementNotFoundException>(() => cut.Find(".emoji-picker"));
    }

    // ===== BADGE EXISTANT (CLICK SUR REACTION) =====

    [Fact]
    public async Task MessageReactions_WhenExistingReactionClicked_ShouldInvokeOnReactWithEmoji()
    {
        // Arrange
        string? selectedEmoji = null;
        var reactions = new List<MessageReactionDto>
        {
            new() { Emoji = "👍", Count = 1, UserIds = ["u1"], Usernames = ["bob"] },
        };

        var cut = Render<MessageReactions>(parameters => parameters
            .Add(p => p.Reactions, reactions)
            .Add(p => p.CurrentUserId, CurrentUserId)
            .Add(p => p.OnReact, EventCallback.Factory.Create<string>(this, e => selectedEmoji = e)));

        // Act
        var badge = cut.Find(".reaction-badge");
        await cut.InvokeAsync(() => badge.Click());

        // Assert
        Assert.Equal("👍", selectedEmoji);
    }

    [Fact]
    public async Task MessageReactions_WhenOwnReactionClicked_ShouldInvokeOnReact()
    {
        // Arrange — l'utilisateur clique sur sa propre réaction pour la retirer
        string? invokedEmoji = null;
        var reactions = new List<MessageReactionDto>
        {
            new() { Emoji = "❤️", Count = 1, UserIds = [CurrentUserId], Usernames = ["me"] },
        };

        var cut = Render<MessageReactions>(parameters => parameters
            .Add(p => p.Reactions, reactions)
            .Add(p => p.CurrentUserId, CurrentUserId)
            .Add(p => p.OnReact, EventCallback.Factory.Create<string>(this, e => invokedEmoji = e)));

        // Act
        var badge = cut.Find(".reaction-badge.own");
        await cut.InvokeAsync(() => badge.Click());

        // Assert — le composant transmet l'emoji au parent qui gère le toggle
        Assert.Equal("❤️", invokedEmoji);
    }

    // ===== CLOSEPICKER PUBLIC =====

    [Fact]
    public async Task MessageReactions_ClosePicker_ShouldHidePicker()
    {
        // Arrange
        var cut = Render<MessageReactions>(parameters => parameters
            .Add(p => p.Reactions, [])
            .Add(p => p.CurrentUserId, CurrentUserId));

        // Ouvrir le picker
        await cut.InvokeAsync(() => cut.Find(".add-reaction-btn").Click());
        Assert.NotNull(cut.Find(".emoji-picker"));

        // Act — fermer via la méthode publique
        await cut.InvokeAsync(() => cut.Instance.ClosePicker());

        cut.Render(); // forcer le re-render après la fermeture

        // Assert
        Assert.Throws<ElementNotFoundException>(() => cut.Find(".emoji-picker"));
    }

    // ===== EDGE CASES =====

    [Fact]
    public void MessageReactions_WithZeroCount_ShouldStillRenderBadge()
    {
        // Arrange — cas limite : réaction avec count=0 (cohérence)
        var reactions = new List<MessageReactionDto>
        {
            new() { Emoji = "😂", Count = 0, UserIds = [], Usernames = [] },
        };

        // Act
        var cut = Render<MessageReactions>(parameters => parameters
            .Add(p => p.Reactions, reactions)
            .Add(p => p.CurrentUserId, CurrentUserId));

        // Assert
        var badge = cut.Find(".reaction-badge");
        Assert.Contains("😂", badge.InnerHtml);
        Assert.Contains("0", badge.InnerHtml);
    }

    [Fact]
    public void MessageReactions_WithManyReactions_ShouldRenderAll()
    {
        // Arrange
        var reactions = Enumerable.Range(1, 8).Select(i => new MessageReactionDto
        {
            Emoji = $"emoji-{i}",
            Count = i,
            UserIds = [$"u{i}"],
            Usernames = [$"user{i}"],
        }).ToList();

        // Act
        var cut = Render<MessageReactions>(parameters => parameters
            .Add(p => p.Reactions, reactions)
            .Add(p => p.CurrentUserId, CurrentUserId));

        // Assert
        var badges = cut.FindAll(".reaction-badge");
        Assert.Equal(8, badges.Count);
    }

    [Fact]
    public void MessageReactions_WithEmptyCurrentUserId_ShouldNotMarkOwnReaction()
    {
        // Arrange
        var reactions = new List<MessageReactionDto>
        {
            new() { Emoji = "👍", Count = 1, UserIds = [Guid.NewGuid().ToString()], Usernames = ["anonymous"] },
        };

        // Act
        var cut = Render<MessageReactions>(parameters => parameters
            .Add(p => p.Reactions, reactions)
            .Add(p => p.CurrentUserId, string.Empty));

        // Assert — pas de classe "own" car userId vide
        var badge = cut.Find(".reaction-badge");
        Assert.DoesNotContain("own", badge.ClassList);
    }

    [Fact]
    public async Task MessageReactions_MultipleReactions_EachClickInvokesCorrectEmoji()
    {
        // Arrange
        var invokedEmojis = new List<string>();
        var reactions = new List<MessageReactionDto>
        {
            new() { Emoji = "👍", Count = 1, UserIds = ["u1"], Usernames = ["alice"] },
            new() { Emoji = "😂", Count = 2, UserIds = ["u2", "u3"], Usernames = ["bob", "charlie"] },
        };

        var cut = Render<MessageReactions>(parameters => parameters
            .Add(p => p.Reactions, reactions)
            .Add(p => p.CurrentUserId, CurrentUserId)
            .Add(p => p.OnReact, EventCallback.Factory.Create<string>(this, e => invokedEmojis.Add(e))));

        // Act — cliquer sur chaque badge
        var badges = cut.FindAll(".reaction-badge");
        await cut.InvokeAsync(() => badges[0].Click());
        badges = cut.FindAll(".reaction-badge");
        await cut.InvokeAsync(() => badges[1].Click());

        // Assert
        Assert.Equal(2, invokedEmojis.Count);
        Assert.Equal("👍", invokedEmojis[0]);
        Assert.Equal("😂", invokedEmojis[1]);
    }

    [Fact]
    public async Task MessageReactions_WhenPickerOpen_ShouldContainSeparator()
    {
        // Arrange
        var cut = Render<MessageReactions>(parameters => parameters
            .Add(p => p.Reactions, [])
            .Add(p => p.CurrentUserId, CurrentUserId));

        await cut.InvokeAsync(() => cut.Find(".add-reaction-btn").Click());

        // Assert — séparateur entre accès rapide et picker complet
        Assert.NotNull(cut.Find(".picker-separator"));
    }
}
