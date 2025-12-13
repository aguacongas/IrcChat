using IrcChat.Client.Components;
using IrcChat.Shared.Models;
using Microsoft.AspNetCore.Components;

namespace IrcChat.Client.Tests.Components;

public partial class MessageListTests
{
    [Fact]
    public void MessageList_WhenCanManageTrue_ShouldShowDeleteButton()
    {
        // Arrange
        var messages = new List<Message>
    {
        new()
        {
            Id = Guid.NewGuid(),
            Username = "otheruser",
            Content = "Test message",
            Timestamp = DateTime.UtcNow,
        },
    };

        // Act
        var cut = Render<MessageList>(parameters => parameters
            .Add(p => p.Messages, messages)
            .Add(p => p.CurrentUsername, "currentuser")
            .Add(p => p.CanManage, true));

        // Assert
        var deleteButton = cut.Find(".btn-delete-message");
        Assert.NotNull(deleteButton);
    }

    [Fact]
    public void MessageList_WhenCanManageFalse_ShouldNotShowDeleteButton()
    {
        // Arrange
        var messages = new List<Message>
    {
        new()
        {
            Id = Guid.NewGuid(),
            Username = "otheruser",
            Content = "Test message",
            Timestamp = DateTime.UtcNow,
        },
    };

        // Act
        var cut = Render<MessageList>(parameters => parameters
            .Add(p => p.Messages, messages)
            .Add(p => p.CurrentUsername, "currentuser")
            .Add(p => p.CanManage, false));

        // Assert
        Assert.Throws<Bunit.ElementNotFoundException>(() => cut.Find(".btn-delete-message"));
    }

    [Fact]
    public async Task MessageList_DeleteButton_WhenClicked_ShouldInvokeCallback()
    {
        // Arrange
        var messageId = Guid.NewGuid();
        var messages = new List<Message>
    {
        new()
        {
            Id = messageId,
            Username = "otheruser",
            Content = "Message to delete",
            Timestamp = DateTime.UtcNow,
        },
    };

        Guid? deletedId = null;
        var cut = Render<MessageList>(parameters => parameters
            .Add(p => p.Messages, messages)
            .Add(p => p.CurrentUsername, "currentuser")
            .Add(p => p.CanManage, true)
            .Add(p => p.OnDeleteMessage, EventCallback.Factory.Create<Guid>(this, id => deletedId = id)));

        // Act
        var deleteButton = cut.Find(".btn-delete-message");
        await cut.InvokeAsync(() => deleteButton.Click());

        // Assert
        Assert.Equal(messageId, deletedId);
    }

    [Fact]
    public async Task MessageList_DeleteButton_ShouldPassCorrectMessageId()
    {
        // Arrange
        var message1Id = Guid.NewGuid();
        var message2Id = Guid.NewGuid();
        var messages = new List<Message>
    {
        new()
        {
            Id = message1Id,
            Username = "user1",
            Content = "Message 1",
            Timestamp = DateTime.UtcNow,
        },
        new()
        {
            Id = message2Id,
            Username = "user2",
            Content = "Message 2",
            Timestamp = DateTime.UtcNow,
        },
    };

        Guid? deletedId = null;
        var cut = Render<MessageList>(parameters => parameters
            .Add(p => p.Messages, messages)
            .Add(p => p.CurrentUsername, "currentuser")
            .Add(p => p.CanManage, true)
            .Add(p => p.OnDeleteMessage, EventCallback.Factory.Create<Guid>(this, id => deletedId = id)));

        // Act - Cliquer sur le deuxième bouton de suppression
        var deleteButtons = cut.FindAll(".btn-delete-message");
        await cut.InvokeAsync(() => deleteButtons[1].Click());

        // Assert
        Assert.Equal(message2Id, deletedId);
    }

    [Fact]
    public void MessageList_EmptyMessages_ShouldNotShowDeleteButtons()
    {
        // Arrange
        var messages = new List<Message>();

        // Act
        var cut = Render<MessageList>(parameters => parameters
            .Add(p => p.Messages, messages)
            .Add(p => p.CurrentUsername, "currentuser")
            .Add(p => p.CanManage, true));

        // Assert
        Assert.Throws<Bunit.ElementNotFoundException>(() => cut.Find(".btn-delete-message"));
    }

    [Fact]
    public void MessageList_DeleteButton_ShouldHaveCorrectTitle()
    {
        // Arrange
        var messages = new List<Message>
    {
        new()
        {
            Id = Guid.NewGuid(),
            Username = "otheruser",
            Content = "Test message",
            Timestamp = DateTime.UtcNow,
        },
    };

        // Act
        var cut = Render<MessageList>(parameters => parameters
            .Add(p => p.Messages, messages)
            .Add(p => p.CurrentUsername, "currentuser")
            .Add(p => p.CanManage, true));

        // Assert
        var deleteButton = cut.Find(".btn-delete-message");
        Assert.Equal("Supprimer ce message", deleteButton.GetAttribute("title"));
    }

    [Fact]
    public async Task MessageList_DeleteButton_MultipleClicks_ShouldInvokeCallbackMultipleTimes()
    {
        // Arrange
        var messageId = Guid.NewGuid();
        var messages = new List<Message>
    {
        new()
        {
            Id = messageId,
            Username = "otheruser",
            Content = "Test message",
            Timestamp = DateTime.UtcNow,
        },
    };

        var clickCount = 0;
        var cut = Render<MessageList>(parameters => parameters
            .Add(p => p.Messages, messages)
            .Add(p => p.CurrentUsername, "currentuser")
            .Add(p => p.CanManage, true)
            .Add(p => p.OnDeleteMessage, EventCallback.Factory.Create<Guid>(this, _ => clickCount++)));

        // Act
        var deleteButton = cut.Find(".btn-delete-message");
        await cut.InvokeAsync(() => deleteButton.Click());
        await cut.InvokeAsync(() => deleteButton.Click());
        await cut.InvokeAsync(() => deleteButton.Click());

        // Assert
        Assert.Equal(3, clickCount);
    }

    [Fact]
    public void MessageList_WhenCanManageChanges_ShouldUpdateDeleteButtonVisibility()
    {
        // Arrange
        var messages = new List<Message>
    {
        new()
        {
            Id = Guid.NewGuid(),
            Username = "otheruser",
            Content = "Test message",
            Timestamp = DateTime.UtcNow,
        },
    };

        var cut = Render<MessageList>(parameters => parameters
            .Add(p => p.Messages, messages)
            .Add(p => p.CurrentUsername, "currentuser")
            .Add(p => p.CanManage, false));

        // Assert - Pas de bouton initialement
        Assert.Throws<Bunit.ElementNotFoundException>(() => cut.Find(".btn-delete-message"));

        // Act - Changer CanManage à true
        cut.Render(parameters => parameters
            .Add(p => p.CanManage, true));

        // Assert - Le bouton devrait maintenant être visible
        var deleteButton = cut.Find(".btn-delete-message");
        Assert.NotNull(deleteButton);
    }

    [Fact]
    public void MessageList_WithMentionedMessage_ShouldStillShowDeleteButton()
    {
        // Arrange
        var messages = new List<Message>
    {
        new()
        {
            Id = Guid.NewGuid(),
            Username = "otheruser",
            Content = "Hey @currentuser, check this out!",
            Timestamp = DateTime.UtcNow,
        },
    };

        // Act
        var cut = Render<MessageList>(parameters => parameters
            .Add(p => p.Messages, messages)
            .Add(p => p.CurrentUsername, "currentuser")
            .Add(p => p.CanManage, true));

        // Assert
        var deleteButton = cut.Find(".btn-delete-message");
        Assert.NotNull(deleteButton);

        // Vérifier que le message est bien mentionné
        var messageDiv = cut.Find(".message");
        Assert.Contains("mentioned", messageDiv.ClassList);
    }
}
