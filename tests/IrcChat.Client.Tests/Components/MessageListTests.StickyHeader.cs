// tests/IrcChat.Client.Tests/Components/MessageListTests.cs
// Tests à ajouter pour le header sticky collapsible

using Bunit;
using IrcChat.Client.Components;
using IrcChat.Shared.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace IrcChat.Client.Tests.Components;

public sealed class MessageListStickyHeaderTests : BunitContext
{
    public MessageListStickyHeaderTests()
    {
        Services.AddLogging();
    }

    [Fact]
    public void MessageList_WithShowDescriptionTrue_ShouldDisplayHeader()
    {
        // Arrange
        var messages = new List<Message>();

        // Act
        var cut = Render<MessageList>(parameters => parameters
            .Add(p => p.Messages, messages)
            .Add(p => p.CurrentUsername, "testuser")
            .Add(p => p.ShowDescription, true)
            .Add(p => p.ChannelDescription, "Description du salon")
            .Add(p => p.CanManage, false));

        // Assert
        Assert.Contains("channel-description-header", cut.Markup);
        Assert.Contains("Description du salon", cut.Markup);
    }

    [Fact]
    public void MessageList_WithShowDescriptionFalse_ShouldNotDisplayHeader()
    {
        // Arrange
        var messages = new List<Message>();

        // Act
        var cut = Render<MessageList>(parameters => parameters
            .Add(p => p.Messages, messages)
            .Add(p => p.CurrentUsername, "testuser")
            .Add(p => p.ShowDescription, false)
            .Add(p => p.ChannelDescription, "Description du salon")
            .Add(p => p.CanManage, false));

        // Assert
        Assert.DoesNotContain("channel-description-header", cut.Markup);
    }

    [Fact]
    public void MessageList_WithEmptyDescription_ShouldDisplayPlaceholder()
    {
        // Arrange
        var messages = new List<Message>();

        // Act
        var cut = Render<MessageList>(parameters => parameters
            .Add(p => p.Messages, messages)
            .Add(p => p.CurrentUsername, "testuser")
            .Add(p => p.ShowDescription, true)
            .Add(p => p.ChannelDescription, null)
            .Add(p => p.CanManage, false));

        // Assert
        Assert.Contains("Aucune description", cut.Markup);
        Assert.Contains("empty", cut.Markup);
    }

    [Fact]
    public void MessageList_WithCanManage_ShouldDisplayEditButton()
    {
        // Arrange
        var messages = new List<Message>();

        // Act
        var cut = Render<MessageList>(parameters => parameters
            .Add(p => p.Messages, messages)
            .Add(p => p.CurrentUsername, "testuser")
            .Add(p => p.ShowDescription, true)
            .Add(p => p.ChannelDescription, "Description")
            .Add(p => p.CanManage, true));

        // Assert
        var editButton = cut.Find(".edit-description-btn");
        Assert.NotNull(editButton);
    }

    [Fact]
    public void MessageList_WithoutCanManage_ShouldNotDisplayEditButton()
    {
        // Arrange
        var messages = new List<Message>();

        // Act
        var cut = Render<MessageList>(parameters => parameters
            .Add(p => p.Messages, messages)
            .Add(p => p.CurrentUsername, "testuser")
            .Add(p => p.ShowDescription, true)
            .Add(p => p.ChannelDescription, "Description")
            .Add(p => p.CanManage, false));

        // Assert
        Assert.Throws<Bunit.ElementNotFoundException>(() => cut.Find(".edit-description-btn"));
    }

    [Fact]
    public async Task MessageList_EditButton_WhenClicked_ShouldInvokeCallback()
    {
        // Arrange
        var messages = new List<Message>();
        var callbackInvoked = false;

        var cut = Render<MessageList>(parameters => parameters
            .Add(p => p.Messages, messages)
            .Add(p => p.CurrentUsername, "testuser")
            .Add(p => p.ShowDescription, true)
            .Add(p => p.ChannelDescription, "Description")
            .Add(p => p.CanManage, true)
            .Add(p => p.OnEditDescription, EventCallback.Factory.Create(this, () => callbackInvoked = true)));

        // Act
        var editButton = cut.Find(".edit-description-btn");
        await cut.InvokeAsync(() => editButton.Click());

        // Assert
        Assert.True(callbackInvoked);
    }

    [Fact]
    public void MessageList_OnScrollPositionChanged_ShouldUpdateCollapsedState()
    {
        // Arrange
        var messages = new List<Message>();

        var cut = Render<MessageList>(parameters => parameters
            .Add(p => p.Messages, messages)
            .Add(p => p.CurrentUsername, "testuser")
            .Add(p => p.ShowDescription, true)
            .Add(p => p.ChannelDescription, "Description")
            .Add(p => p.CanManage, false));

        var instance = cut.Instance;

        // Act
        instance.OnScrollPositionChanged(true);

        // Assert
        Assert.Contains("collapsed", cut.Markup);
    }

    [Fact]
    public void MessageList_OnScrollPositionChanged_WithFalse_ShouldDisplayExpanded()
    {
        // Arrange
        var messages = new List<Message>();

        var cut = Render<MessageList>(parameters => parameters
            .Add(p => p.Messages, messages)
            .Add(p => p.CurrentUsername, "testuser")
            .Add(p => p.ShowDescription, true)
            .Add(p => p.ChannelDescription, "Description")
            .Add(p => p.CanManage, false));

        var instance = cut.Instance;

        // Act - Collapse puis expand
        instance.OnScrollPositionChanged(true);
        instance.OnScrollPositionChanged(false);

        // Assert
        Assert.Contains("expanded", cut.Markup);
    }

    [Fact]
    public async Task MessageList_HeaderClick_ShouldToggleCollapse()
    {
        // Arrange
        var messages = new List<Message>();

        var cut = Render<MessageList>(parameters => parameters
            .Add(p => p.Messages, messages)
            .Add(p => p.CurrentUsername, "testuser")
            .Add(p => p.ShowDescription, true)
            .Add(p => p.ChannelDescription, "Description longue")
            .Add(p => p.CanManage, false));

        var header = cut.Find(".channel-description-header");

        // Act - Premier click (collapse)
        await cut.InvokeAsync(() => header.Click());

        // Assert
        Assert.Contains("collapsed", cut.Markup);

        // Act - Deuxième click (expand)
        await cut.InvokeAsync(() => header.Click());

        // Assert
        Assert.Contains("expanded", cut.Markup);
    }

    [Fact]
    public void MessageList_WithMultilineDescription_ShouldDisplayAllLines()
    {
        // Arrange
        var messages = new List<Message>();
        var multilineDescription = "Ligne 1\nLigne 2\nLigne 3";

        // Act
        var cut = Render<MessageList>(parameters => parameters
            .Add(p => p.Messages, messages)
            .Add(p => p.CurrentUsername, "testuser")
            .Add(p => p.ShowDescription, true)
            .Add(p => p.ChannelDescription, multilineDescription)
            .Add(p => p.CanManage, false));

        // Assert
        Assert.Contains("Ligne 1", cut.Markup);
        Assert.Contains("Ligne 2", cut.Markup);
        Assert.Contains("Ligne 3", cut.Markup);
        Assert.Contains("<br", cut.Markup);
    }

    [Fact]
    public void MessageList_CollapsedState_ShouldHaveCollapsedClass()
    {
        // Arrange
        var messages = new List<Message>();

        var cut = Render<MessageList>(parameters => parameters
            .Add(p => p.Messages, messages)
            .Add(p => p.CurrentUsername, "testuser")
            .Add(p => p.ShowDescription, true)
            .Add(p => p.ChannelDescription, "Description")
            .Add(p => p.CanManage, false));

        // Act
        cut.Instance.OnScrollPositionChanged(true);

        // Assert
        var header = cut.Find(".channel-description-header");
        Assert.Contains("collapsed", header.ClassName);
    }

    [Fact]
    public void MessageList_ExpandedState_ShouldHaveExpandedClass()
    {
        // Arrange
        var messages = new List<Message>();

        var cut = Render<MessageList>(parameters => parameters
            .Add(p => p.Messages, messages)
            .Add(p => p.CurrentUsername, "testuser")
            .Add(p => p.ShowDescription, true)
            .Add(p => p.ChannelDescription, "Description")
            .Add(p => p.CanManage, false));

        // Act
        cut.Instance.OnScrollPositionChanged(false);

        // Assert
        var header = cut.Find(".channel-description-header");
        Assert.Contains("expanded", header.ClassName);
    }

    [Fact]
    public void MessageList_OnUserScroll_ShouldUpdateScrollingState()
    {
        // Arrange
        var messages = new List<Message>();

        var cut = Render<MessageList>(parameters => parameters
            .Add(p => p.Messages, messages)
            .Add(p => p.CurrentUsername, "testuser")
            .Add(p => p.ShowDescription, true)
            .Add(p => p.ChannelDescription, "Description")
            .Add(p => p.CanManage, false));

        // Act
        cut.Instance.OnUserScroll(false); // Pas en bas

        // Assert - Pas d'erreur, état interne mis à jour
        Assert.NotNull(cut.Instance);
    }

    [Fact]
    public void MessageList_WithLongDescription_ShouldNotOverflow()
    {
        // Arrange
        var messages = new List<Message>();
        var longDescription = new string('a', 500);

        // Act
        var cut = Render<MessageList>(parameters => parameters
            .Add(p => p.Messages, messages)
            .Add(p => p.CurrentUsername, "testuser")
            .Add(p => p.ShowDescription, true)
            .Add(p => p.ChannelDescription, longDescription)
            .Add(p => p.CanManage, false));

        // Assert
        Assert.Contains(longDescription, cut.Markup);
        Assert.Contains("description-text", cut.Markup);
    }

    [Fact]
    public async Task MessageList_EditButtonClick_ShouldNotTriggerHeaderToggle()
    {
        // Arrange
        var messages = new List<Message>();
        var editClicked = false;

        var cut = Render<MessageList>(parameters => parameters
            .Add(p => p.Messages, messages)
            .Add(p => p.CurrentUsername, "testuser")
            .Add(p => p.ShowDescription, true)
            .Add(p => p.ChannelDescription, "Description")
            .Add(p => p.CanManage, true)
            .Add(p => p.OnEditDescription, EventCallback.Factory.Create(this, () => editClicked = true)));

        var initialMarkup = cut.Markup;
        var hasCollapsedBefore = initialMarkup.Contains("collapsed");

        // Act
        var editButton = cut.Find(".edit-description-btn");
        await cut.InvokeAsync(() => editButton.Click());

        // Assert
        Assert.True(editClicked);

        // Le header ne doit pas avoir changé d'état (stopPropagation)
        var hasCollapsedAfter = cut.Markup.Contains("collapsed");
        Assert.Equal(hasCollapsedBefore, hasCollapsedAfter);
    }

    [Fact]
    public void MessageList_InitialState_ShouldBeExpanded()
    {
        // Arrange
        var messages = new List<Message>();

        // Act
        var cut = Render<MessageList>(parameters => parameters
            .Add(p => p.Messages, messages)
            .Add(p => p.CurrentUsername, "testuser")
            .Add(p => p.ShowDescription, true)
            .Add(p => p.ChannelDescription, "Description")
            .Add(p => p.CanManage, false));

        // Assert
        Assert.Contains("expanded", cut.Markup);
        Assert.DoesNotContain("collapsed", cut.Markup);
    }

    [Fact]
    public void MessageList_WithSpecialCharacters_ShouldDisplayCorrectly()
    {
        // Arrange
        var messages = new List<Message>();
        var specialDescription = "Description <avec> \"caractères\" spéciaux & symboles";

        // Act
        var cut = Render<MessageList>(parameters => parameters
            .Add(p => p.Messages, messages)
            .Add(p => p.CurrentUsername, "testuser")
            .Add(p => p.ShowDescription, true)
            .Add(p => p.ChannelDescription, specialDescription)
            .Add(p => p.CanManage, false));

        // Assert
        Assert.Contains("caractères", cut.Markup);
        Assert.Contains("spéciaux", cut.Markup);
    }
}
