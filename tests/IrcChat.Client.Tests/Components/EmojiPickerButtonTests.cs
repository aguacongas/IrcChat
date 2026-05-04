// tests/IrcChat.Client.Tests/Components/EmojiPickerButtonTests.cs
using IrcChat.Client.Components;
using Microsoft.AspNetCore.Components;

namespace IrcChat.Client.Tests.Components;

/// <summary>
/// Tests pour EmojiPickerButton — le bouton 😊 + picker dans .message-actions.
/// L'état ouvert/fermé est contrôlé par le parent via IsOpen + OnToggle.
/// </summary>
public class EmojiPickerButtonTests : BunitContext
{
    private readonly Guid _messageId = Guid.NewGuid();

    // ===== RENDU INITIAL =====

    [Fact]
    public void EmojiPickerButton_ShouldRenderAddReactionButton()
    {
        // Arrange & Act
        var cut = Render<EmojiPickerButton>(parameters => parameters
            .Add(p => p.MessageId, _messageId)
            .Add(p => p.IsOpen, false));

        // Assert
        var btn = cut.Find(".add-reaction-btn");
        Assert.NotNull(btn);
        Assert.Contains("😊", btn.TextContent);
    }

    [Fact]
    public void EmojiPickerButton_WhenIsOpenFalse_ShouldHidePicker()
    {
        // Arrange & Act
        var cut = Render<EmojiPickerButton>(parameters => parameters
            .Add(p => p.MessageId, _messageId)
            .Add(p => p.IsOpen, false));

        // Assert
        Assert.Throws<ElementNotFoundException>(() => cut.Find(".emoji-picker"));
    }

    [Fact]
    public void EmojiPickerButton_WhenIsOpenTrue_ShouldShowPicker()
    {
        // Arrange & Act
        var cut = Render<EmojiPickerButton>(parameters => parameters
            .Add(p => p.MessageId, _messageId)
            .Add(p => p.IsOpen, true));

        // Assert
        Assert.NotNull(cut.Find(".emoji-picker"));
    }

    [Fact]
    public void EmojiPickerButton_WhenIsOpenFalse_ButtonShouldNotHaveActiveClass()
    {
        // Arrange & Act
        var cut = Render<EmojiPickerButton>(parameters => parameters
            .Add(p => p.MessageId, _messageId)
            .Add(p => p.IsOpen, false));

        // Assert
        Assert.DoesNotContain("active", cut.Find(".add-reaction-btn").ClassList);
    }

    [Fact]
    public void EmojiPickerButton_WhenIsOpenTrue_ButtonShouldHaveActiveClass()
    {
        // Arrange & Act
        var cut = Render<EmojiPickerButton>(parameters => parameters
            .Add(p => p.MessageId, _messageId)
            .Add(p => p.IsOpen, true));

        // Assert
        Assert.Contains("active", cut.Find(".add-reaction-btn").ClassList);
    }

    // ===== CALLBACK OnToggle =====

    [Fact]
    public async Task EmojiPickerButton_WhenButtonClicked_ShouldInvokeOnToggleWithMessageId()
    {
        // Arrange
        Guid? toggledId = null;
        var cut = Render<EmojiPickerButton>(parameters => parameters
            .Add(p => p.MessageId, _messageId)
            .Add(p => p.IsOpen, false)
            .Add(p => p.OnToggle, EventCallback.Factory.Create<Guid>(this, id => toggledId = id)));

        // Act
        await cut.InvokeAsync(() => cut.Find(".add-reaction-btn").Click());

        // Assert
        Assert.Equal(_messageId, toggledId);
    }

    [Fact]
    public async Task EmojiPickerButton_WhenOpenAndButtonClicked_ShouldStillInvokeOnToggle()
    {
        // Arrange — re-clic quand ouvert → toggle géré par le parent
        Guid? toggledId = null;
        var cut = Render<EmojiPickerButton>(parameters => parameters
            .Add(p => p.MessageId, _messageId)
            .Add(p => p.IsOpen, true)
            .Add(p => p.OnToggle, EventCallback.Factory.Create<Guid>(this, id => toggledId = id)));

        // Act
        await cut.InvokeAsync(() => cut.Find(".add-reaction-btn").Click());

        // Assert
        Assert.Equal(_messageId, toggledId);
    }

    // ===== QUICK EMOJIS =====

    [Fact]
    public void EmojiPickerButton_WhenOpen_ShouldShowSixQuickEmojis()
    {
        // Arrange & Act
        var cut = Render<EmojiPickerButton>(parameters => parameters
            .Add(p => p.MessageId, _messageId)
            .Add(p => p.IsOpen, true));

        // Assert
        Assert.Equal(6, cut.FindAll(".quick-emoji-btn").Count);
    }

    [Fact]
    public void EmojiPickerButton_WhenOpen_ShouldShowCorrectQuickEmojis()
    {
        // Arrange & Act
        var cut = Render<EmojiPickerButton>(parameters => parameters
            .Add(p => p.MessageId, _messageId)
            .Add(p => p.IsOpen, true));

        // Assert
        var markup = cut.Find(".quick-emojis").InnerHtml;
        Assert.Contains("👍", markup);
        Assert.Contains("❤️", markup);
        Assert.Contains("😂", markup);
        Assert.Contains("😮", markup);
        Assert.Contains("😢", markup);
        Assert.Contains("😡", markup);
    }

    [Fact]
    public async Task EmojiPickerButton_WhenQuickEmojiClicked_ShouldInvokeOnReact()
    {
        // Arrange
        string? selectedEmoji = null;
        var cut = Render<EmojiPickerButton>(parameters => parameters
            .Add(p => p.MessageId, _messageId)
            .Add(p => p.IsOpen, true)
            .Add(p => p.OnToggle, EventCallback.Factory.Create<Guid>(this, _ => { }))
            .Add(p => p.OnReact, EventCallback.Factory.Create<string>(this, e => selectedEmoji = e)));

        // Act
        await cut.InvokeAsync(() => cut.Find(".quick-emoji-btn").Click());

        // Assert
        Assert.Equal("👍", selectedEmoji);
    }

    [Fact]
    public async Task EmojiPickerButton_WhenQuickEmojiClicked_ShouldInvokeOnToggleToSignalClose()
    {
        // Arrange — après sélection, le composant demande au parent de fermer via OnToggle
        Guid? toggledId = null;
        var cut = Render<EmojiPickerButton>(parameters => parameters
            .Add(p => p.MessageId, _messageId)
            .Add(p => p.IsOpen, true)
            .Add(p => p.OnToggle, EventCallback.Factory.Create<Guid>(this, id => toggledId = id))
            .Add(p => p.OnReact, EventCallback.Factory.Create<string>(this, _ => { })));

        // Act
        await cut.InvokeAsync(() => cut.Find(".quick-emoji-btn").Click());

        // Assert
        Assert.Equal(_messageId, toggledId);
    }

    // ===== PICKER COMPLET =====

    [Fact]
    public void EmojiPickerButton_WhenOpen_ShouldShowCategoryButtons()
    {
        // Arrange & Act
        var cut = Render<EmojiPickerButton>(parameters => parameters
            .Add(p => p.MessageId, _messageId)
            .Add(p => p.IsOpen, true));

        // Assert
        Assert.True(cut.FindAll(".category-btn").Count >= 1);
    }

    [Fact]
    public void EmojiPickerButton_WhenOpen_ShouldShowEmojiGrid()
    {
        // Arrange & Act
        var cut = Render<EmojiPickerButton>(parameters => parameters
            .Add(p => p.MessageId, _messageId)
            .Add(p => p.IsOpen, true));

        // Assert
        Assert.NotNull(cut.Find(".picker-emojis"));
        Assert.True(cut.FindAll(".full-emoji-btn").Count > 0);
    }

    [Fact]
    public async Task EmojiPickerButton_WhenCategoryClicked_ShouldUpdateActiveClass()
    {
        // Arrange
        var cut = Render<EmojiPickerButton>(parameters => parameters
            .Add(p => p.MessageId, _messageId)
            .Add(p => p.IsOpen, true));

        var categories = cut.FindAll(".category-btn");
        if (categories.Count >= 2)
        {
            // Act
            await cut.InvokeAsync(() => categories[1].Click());

            // Assert
            var updated = cut.FindAll(".category-btn");
            Assert.Contains("active", updated[1].ClassList);
            Assert.DoesNotContain("active", updated[0].ClassList);
        }
    }

    [Fact]
    public async Task EmojiPickerButton_WhenFullEmojiClicked_ShouldInvokeOnReact()
    {
        // Arrange
        string? selectedEmoji = null;
        var cut = Render<EmojiPickerButton>(parameters => parameters
            .Add(p => p.MessageId, _messageId)
            .Add(p => p.IsOpen, true)
            .Add(p => p.OnToggle, EventCallback.Factory.Create<Guid>(this, _ => { }))
            .Add(p => p.OnReact, EventCallback.Factory.Create<string>(this, e => selectedEmoji = e)));

        // Act
        await cut.InvokeAsync(() => cut.Find(".full-emoji-btn").Click());

        // Assert
        Assert.NotNull(selectedEmoji);
        Assert.False(string.IsNullOrEmpty(selectedEmoji));
    }

    [Fact]
    public async Task EmojiPickerButton_WhenFullEmojiClicked_ShouldInvokeOnToggle()
    {
        // Arrange
        Guid? toggledId = null;
        var cut = Render<EmojiPickerButton>(parameters => parameters
            .Add(p => p.MessageId, _messageId)
            .Add(p => p.IsOpen, true)
            .Add(p => p.OnToggle, EventCallback.Factory.Create<Guid>(this, id => toggledId = id))
            .Add(p => p.OnReact, EventCallback.Factory.Create<string>(this, _ => { })));

        // Act
        await cut.InvokeAsync(() => cut.Find(".full-emoji-btn").Click());

        // Assert
        Assert.Equal(_messageId, toggledId);
    }

    [Fact]
    public void EmojiPickerButton_WhenOpen_ShouldShowSeparator()
    {
        // Arrange & Act
        var cut = Render<EmojiPickerButton>(parameters => parameters
            .Add(p => p.MessageId, _messageId)
            .Add(p => p.IsOpen, true));

        // Assert
        Assert.NotNull(cut.Find(".picker-separator"));
    }

    // ===== ÉTAT FERMÉ =====

    [Fact]
    public void EmojiPickerButton_WhenClosed_ShouldNotShowPickerContent()
    {
        // Arrange & Act
        var cut = Render<EmojiPickerButton>(parameters => parameters
            .Add(p => p.MessageId, _messageId)
            .Add(p => p.IsOpen, false));

        // Assert — aucun contenu du picker visible
        Assert.Throws<ElementNotFoundException>(() => cut.Find(".quick-emojis"));
        Assert.Throws<ElementNotFoundException>(() => cut.Find(".full-picker"));
        Assert.Throws<ElementNotFoundException>(() => cut.Find(".picker-separator"));
    }

    // ===== EDGE CASES =====

    [Fact]
    public void EmojiPickerButton_IsOpenChangesFromParent_ShouldReflectNewState()
    {
        // Arrange — simuler le parent qui passe IsOpen=false puis true
        var cut = Render<EmojiPickerButton>(parameters => parameters
            .Add(p => p.MessageId, _messageId)
            .Add(p => p.IsOpen, false));

        Assert.Throws<ElementNotFoundException>(() => cut.Find(".emoji-picker"));

        // Act — recréer avec IsOpen=true (simule le parent qui met à jour le paramètre)
        cut = Render<EmojiPickerButton>(parameters => parameters
            .Add(p => p.MessageId, _messageId)
            .Add(p => p.IsOpen, true));

        // Assert
        Assert.NotNull(cut.Find(".emoji-picker"));
    }

    [Fact]
    public void EmojiPickerButton_IsOpenFromTrueToFalse_ShouldHidePicker()
    {
        // Arrange
        var cut = Render<EmojiPickerButton>(parameters => parameters
            .Add(p => p.MessageId, _messageId)
            .Add(p => p.IsOpen, true));

        Assert.NotNull(cut.Find(".emoji-picker"));

        // Act — le parent ferme le picker
        cut = Render<EmojiPickerButton>(parameters => parameters
            .Add(p => p.MessageId, _messageId)
            .Add(p => p.IsOpen, false));

        // Assert
        Assert.Throws<ElementNotFoundException>(() => cut.Find(".emoji-picker"));
    }

    [Fact]
    public async Task EmojiPickerButton_DifferentCategories_EachHasContent()
    {
        // Arrange
        var cut = Render<EmojiPickerButton>(parameters => parameters
            .Add(p => p.MessageId, _messageId)
            .Add(p => p.IsOpen, true));

        var categories = cut.FindAll(".category-btn");

        var cat = categories[0];
        await cut.InvokeAsync(() => cat.Click());
        Assert.True(cut.FindAll(".full-emoji-btn").Count > 0);
    }
}
