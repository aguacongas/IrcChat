using IrcChat.Client.Components;
using IrcChat.Client.Models;
using IrcChat.Client.Services;
using IrcChat.Shared.Models;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;

namespace IrcChat.Client.Tests.Components;

public partial class MessageInputTests : BunitContext
{
    private readonly Mock<IEmojiService> _emojiServiceMock;

    public MessageInputTests()
    {
        _emojiServiceMock = new Mock<IEmojiService>();

        _emojiServiceMock.Setup(x => x.IsLoaded).Returns(true);
        _emojiServiceMock.Setup(x => x.GetAllEmojis()).Returns(GetTestEmojis());
        _emojiServiceMock.Setup(x => x.GetCategories()).Returns([new EmojiCategory()]);

        Services.AddSingleton(_emojiServiceMock.Object);
    }

    [Fact]
    public void MessageInput_ShouldRenderCorrectly()
    {
        // Arrange & Act
        var cut = Render<MessageInput>(parameters => parameters
            .Add(p => p.IsConnected, true)
            .Add(p => p.AvailableUsers, []));

        // Assert
        var input = cut.Find("input");
        Assert.NotNull(input);
        Assert.Equal("Tapez votre message... (@ pour mentionner, : pour emoji)", input.GetAttribute("placeholder"));
        Assert.False(input.HasAttribute("disabled"));
    }

    [Fact]
    public void MessageInput_ShouldBeDisabledWhenNotConnected()
    {
        // Arrange & Act
        var cut = Render<MessageInput>(parameters => parameters
            .Add(p => p.IsConnected, false)
            .Add(p => p.AvailableUsers, []));

        // Assert
        var input = cut.Find("input");
        Assert.True(input.HasAttribute("disabled"));

        var button = cut.Find("button");
        Assert.True(button.HasAttribute("disabled"));
    }

    [Fact]
    public async Task MessageInput_ShouldSendMessageOnEnterKey()
    {
        // Arrange
        var messageSent = false;
        var sentMessage = string.Empty;

        var cut = Render<MessageInput>(parameters => parameters
            .Add(p => p.IsConnected, true)
            .Add(p => p.AvailableUsers, [])
            .Add(p => p.OnSendMessage, msg =>
            {
                messageSent = true;
                sentMessage = msg;
            }));

        var input = await cut.InvokeAsync(() => cut.Find("input"));

        // Act
        input.Input("Hello World");
        await input.KeyDownAsync(new KeyboardEventArgs { Key = "Enter" });

        // Assert
        Assert.True(messageSent);
        Assert.Equal("Hello World", sentMessage);
    }

    [Fact]
    public async Task MessageInput_ShouldSendMessageOnButtonClick()
    {
        // Arrange
        var messageSent = false;
        var sentMessage = string.Empty;

        var cut = Render<MessageInput>(parameters => parameters
            .Add(p => p.IsConnected, true)
            .Add(p => p.AvailableUsers, [])
            .Add(p => p.OnSendMessage, msg =>
            {
                messageSent = true;
                sentMessage = msg;
            }));

        var input = await cut.InvokeAsync(() => cut.Find("input"));
        var button = await cut.InvokeAsync(() => cut.Find("button"));
        // Act
        input.Input("Test message");
        await button.ClickAsync();

        // Assert
        Assert.True(messageSent);
        Assert.Equal("Test message", sentMessage);
    }

    [Fact]
    public void MessageInput_ShouldNotSendEmptyMessage()
    {
        // Arrange
        var messageSent = false;

        var cut = Render<MessageInput>(parameters => parameters
            .Add(p => p.IsConnected, true)
            .Add(p => p.AvailableUsers, [])
            .Add(p => p.OnSendMessage, msg => messageSent = true));

        var button = cut.Find("button");

        // Act
        button.Click();

        // Assert
        Assert.False(messageSent);
    }

    [Fact]
    public async Task MessageInput_ShouldShowAutocompleteWhenTypingAtSymbol()
    {
        // Arrange
        var users = new List<User>
        {
            new() { Username = "Alice", UserId = "1" },
            new() { Username = "Bob", UserId = "2" },
            new() { Username = "Charlie", UserId = "3" },
        };

        var cut = Render<MessageInput>(parameters => parameters
            .Add(p => p.IsConnected, true)
            .Add(p => p.AvailableUsers, users));

        var input = await cut.InvokeAsync(() => cut.Find("input"));

        // Act
        input.Input("@A");
        await input.KeyUpAsync(new KeyboardEventArgs { Key = "A" });

        // Assert
        var dropdown = cut.Find(".autocomplete-dropdown");
        Assert.NotNull(dropdown);

        var items = cut.FindAll(".autocomplete-item");
        Assert.Single(items); // Seulement Alice correspond
        Assert.Contains("Alice", items[0].TextContent);
    }

    [Fact]
    public async Task MessageInput_ShouldFilterUsersInAutocomplete()
    {
        // Arrange
        var users = new List<User>
        {
            new() { Username = "Alice", UserId = "1" },
            new() { Username = "Alex", UserId = "2" },
            new() { Username = "Bob", UserId = "3" },
        };

        var cut = Render<MessageInput>(parameters => parameters
            .Add(p => p.IsConnected, true)
            .Add(p => p.AvailableUsers, users));

        var input = await cut.InvokeAsync(() => cut.Find("input"));

        // Act
        input.Input("@Al");
        await input.KeyUpAsync(new KeyboardEventArgs { Key = "l" });

        // Assert
        var items = cut.FindAll(".autocomplete-item");
        Assert.Equal(2, items.Count); // Alice et Alex
        Assert.Contains("Alice", items[0].TextContent);
        Assert.Contains("Alex", items[1].TextContent);
    }

    [Fact]
    public async Task MessageInput_ShouldSelectUserWithEnterKey()
    {
        // Arrange
        var users = new List<User>
        {
            new() { Username = "Alice", UserId = "1" },
            new() { Username = "Bob", UserId = "2" },
        };

        var cut = Render<MessageInput>(parameters => parameters
            .Add(p => p.IsConnected, true)
            .Add(p => p.AvailableUsers, users));

        var input = await cut.InvokeAsync(() => cut.Find("input"));

        // Act
        input.Input("@Al");
        await input.KeyUpAsync(new KeyboardEventArgs { Key = "l" });
        await input.KeyDownAsync(new KeyboardEventArgs { Key = "Enter" });

        // Assert
        var inputValue = input.GetAttribute("value");
        Assert.Contains("Alice", inputValue);
        Assert.DoesNotContain("@", inputValue); // Le @ doit être supprimé

        // L'autocomplete doit être masqué
        Assert.Empty(cut.FindAll(".autocomplete-dropdown"));
    }

    [Fact]
    public async Task MessageInput_ShouldNavigateAutocompleteWithArrowKeys()
    {
        // Arrange
        var users = new List<User>
        {
            new() { Username = "Alice", UserId = "1" },
            new() { Username = "Alex", UserId = "2" },
            new() { Username = "Adam", UserId = "3" },
        };

        var cut = Render<MessageInput>(parameters => parameters
            .Add(p => p.IsConnected, true)
            .Add(p => p.AvailableUsers, users));

        var input = await cut.InvokeAsync(() => cut.Find("input"));

        // Act
        input.Input("@A");
        await input.KeyUpAsync(new KeyboardEventArgs { Key = "A" });

        // Naviguer vers le bas
        await input.KeyDownAsync(new KeyboardEventArgs { Key = "ArrowDown" });

        // Assert
        var items = cut.FindAll(".autocomplete-item");
        Assert.Equal(3, items.Count);
        Assert.Contains("selected", items[1].ClassList); // Le deuxième élément doit être sélectionné

        // Naviguer vers le haut
        await input.KeyDownAsync(new KeyboardEventArgs { Key = "ArrowUp" });

        // Assert
        items = cut.FindAll(".autocomplete-item");
        Assert.Equal(3, items.Count);
        Assert.Contains("selected", items[0].ClassList); // Le 1er élément doit être sélectionné
    }

    [Fact]
    public async Task MessageInput_ShouldCloseAutocompleteWithEscape()
    {
        // Arrange
        var users = new List<User>
        {
            new() { Username = "Alice", UserId = "1" },
        };

        var cut = Render<MessageInput>(parameters => parameters
            .Add(p => p.IsConnected, true)
            .Add(p => p.AvailableUsers, users));

        var input = await cut.InvokeAsync(() => cut.Find("input"));

        // Act
        input.Input("@Al");
        await input.KeyUpAsync(new KeyboardEventArgs { Key = "l" });

        // Vérifier que l'autocomplete est visible
        Assert.NotEmpty(cut.FindAll(".autocomplete-dropdown"));

        // Appuyer sur Escape
        await input.KeyDownAsync(new KeyboardEventArgs { Key = "Escape" });

        // Assert
        Assert.Empty(cut.FindAll(".autocomplete-dropdown"));
    }

    [Fact]
    public async Task MessageInput_ShouldCompleteWithTabKey()
    {
        // Arrange
        var users = new List<User>
        {
            new() { Username = "Alice", UserId = "1" },
        };

        var cut = Render<MessageInput>(parameters => parameters
            .Add(p => p.IsConnected, true)
            .Add(p => p.AvailableUsers, users));

        var input = await cut.InvokeAsync(() => cut.Find("input"));

        // Act
        input.Input("Al");
        await input.KeyDownAsync(new KeyboardEventArgs { Key = "Tab" });

        // Assert
        var inputValue = input.GetAttribute("value");
        Assert.Equal("Alice", inputValue);
    }

    [Fact]
    public async Task MessageInput_ShouldNotShowAutocompleteForAtInMiddleOfWord()
    {
        // Arrange
        var users = new List<User>
        {
            new() { Username = "Alice", UserId = "1" },
        };

        var cut = Render<MessageInput>(parameters => parameters
            .Add(p => p.IsConnected, true)
            .Add(p => p.AvailableUsers, users));

        var input = await cut.InvokeAsync(() => cut.Find("input"));

        // Act - @ au milieu d'un mot ne doit pas déclencher l'autocomplete
        input.Input("test@Al");
        await input.KeyUpAsync(new KeyboardEventArgs { Key = "l" });

        // Assert
        Assert.Empty(cut.FindAll(".autocomplete-dropdown"));
    }

    [Fact]
    public void MessageInput_InsertUsername_ShouldAddUsernameWithoutAt()
    {
        // Arrange
        var cut = Render<MessageInput>(parameters => parameters
            .Add(p => p.IsConnected, true)
            .Add(p => p.AvailableUsers, []));

        var input = cut.Find("input");

        // Act
        cut.Instance.InsertUsername("Alice");

        // Assert
        var inputValue = input.GetAttribute("value");
        Assert.Equal("Alice ", inputValue);
        Assert.DoesNotContain("@", inputValue);
    }

    [Fact]
    public async Task MessageInput_InsertUsername_ShouldAppendToExistingText()
    {
        // Arrange
        var cut = Render<MessageInput>(parameters => parameters
            .Add(p => p.IsConnected, true)
            .Add(p => p.AvailableUsers, []));

        var input = await cut.InvokeAsync(() => cut.Find("input"));

        // Act
        input.Input("Hello");
        cut.Instance.InsertUsername("Alice");

        // Assert
        var inputValue = input.GetAttribute("value");
        Assert.Equal("Hello Alice ", inputValue);
    }

    [Fact]
    public async Task MessageInput_ShouldLimitAutocompleteTo5Users()
    {
        // Arrange
        var users = new List<User>();
        for (var i = 1; i <= 10; i++)
        {
            users.Add(new User { Username = $"User{i}", UserId = i.ToString() });
        }

        var cut = Render<MessageInput>(parameters => parameters
            .Add(p => p.IsConnected, true)
            .Add(p => p.AvailableUsers, users));

        var input = await cut.InvokeAsync(() => cut.Find("input"));

        // Act
        input.Input("@User");
        await input.KeyUpAsync(new KeyboardEventArgs { Key = "r" });

        // Assert
        var items = cut.FindAll(".autocomplete-item");
        Assert.Equal(5, items.Count); // Maximum 5 suggestions
    }

    [Fact]
    public async Task MessageInput_FocusAsync_ShouldBeCalled()
    {
        // Arrange
        var cut = Render<MessageInput>(parameters => parameters
            .Add(p => p.IsConnected, true)
            .Add(p => p.AvailableUsers, []));

        // Act
        await cut.Instance.FocusAsync();

        // Assert
        // Pas d'exception levée = succès
        Assert.NotNull(cut.Instance);
    }

    [Fact]
    public async Task MessageInput_WhenNoAvailableUsers_HideAutoComplete()
    {
        // Arrange
        var cut = Render<MessageInput>(parameters => parameters
            .Add(p => p.IsConnected, true)
            .Add(p => p.AvailableUsers, []));

        // Act
        var input = await cut.InvokeAsync(() => cut.Find("input"));

        // Act
        input.Input("@User");
        await input.KeyUpAsync(new KeyboardEventArgs { Key = "r" });

        // Assert
        var items = cut.FindAll(".autocomplete-item");
        Assert.Empty(items);
    }

    [Fact]
    public async Task MessageInput_WhenNullAvailableUsers_HideAutoComplete()
    {
        // Arrange
        var cut = Render<MessageInput>(parameters => parameters
            .Add(p => p.IsConnected, true)
            .Add(p => p.AvailableUsers, null));

        // Act
        var input = await cut.InvokeAsync(() => cut.Find("input"));

        // Act
        input.Input("@User");
        await input.KeyUpAsync(new KeyboardEventArgs { Key = "r" });

        // Assert
        var items = cut.FindAll(".autocomplete-item");
        Assert.Empty(items);
    }

    [Fact]
    public async Task MessageInput_WhenMessageInputEmpty_HideAutoComplete()
    {
        // Arrange
        var users = new List<User>();
        for (var i = 1; i <= 10; i++)
        {
            users.Add(new User { Username = $"User{i}", UserId = i.ToString() });
        }

        var cut = Render<MessageInput>(parameters => parameters
            .Add(p => p.IsConnected, true)
            .Add(p => p.AvailableUsers, users));

        // Act
        var input = await cut.InvokeAsync(() => cut.Find("input"));

        // Act
        await input.KeyUpAsync(new KeyboardEventArgs { Key = "r" });

        // Assert
        var items = cut.FindAll(".autocomplete-item");
        Assert.Empty(items);
    }

    [Fact]
    public async Task MessageInput_WhenNoAtChar_HideAutoComplete()
    {
        // Arrange
        var users = new List<User>();
        for (var i = 1; i <= 10; i++)
        {
            users.Add(new User { Username = $"User{i}", UserId = i.ToString() });
        }

        var cut = Render<MessageInput>(parameters => parameters
            .Add(p => p.IsConnected, true)
            .Add(p => p.AvailableUsers, users));

        // Act
        var input = await cut.InvokeAsync(() => cut.Find("input"));

        // Act
        input.Input("User");
        await input.KeyUpAsync(new KeyboardEventArgs { Key = "r" });

        // Assert
        var items = cut.FindAll(".autocomplete-item");
        Assert.Empty(items);
    }

    [Fact]
    public async Task MessageInput_WhenAtCharNotFirst_HideAutoComplete()
    {
        // Arrange
        var users = new List<User>();
        for (var i = 1; i <= 10; i++)
        {
            users.Add(new User { Username = $"User{i}", UserId = i.ToString() });
        }

        var cut = Render<MessageInput>(parameters => parameters
            .Add(p => p.IsConnected, true)
            .Add(p => p.AvailableUsers, users));

        // Act
        var input = await cut.InvokeAsync(() => cut.Find("input"));

        // Act
        input.Input("user@exemple.com");
        await input.KeyUpAsync(new KeyboardEventArgs { Key = "r" });

        // Assert
        var items = cut.FindAll(".autocomplete-item");
        Assert.Empty(items);
    }

    [Fact]
    public async Task MessageInput_WhenMentionEndWithSpace_HideAutoComplete()
    {
        // Arrange
        var users = new List<User>();
        for (var i = 1; i <= 10; i++)
        {
            users.Add(new User { Username = $"User{i}", UserId = i.ToString() });
        }

        var cut = Render<MessageInput>(parameters => parameters
            .Add(p => p.IsConnected, true)
            .Add(p => p.AvailableUsers, users));

        // Act
        var input = await cut.InvokeAsync(() => cut.Find("input"));

        // Act
        input.Input("@User ");
        await input.KeyUpAsync(new KeyboardEventArgs { Key = "r" });

        // Assert
        var items = cut.FindAll(".autocomplete-item");
        Assert.Empty(items);
    }
}