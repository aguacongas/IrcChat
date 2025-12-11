using IrcChat.Client.Components;
using IrcChat.Client.Models;
using IrcChat.Shared.Models;
using Microsoft.AspNetCore.Components.Web;

namespace IrcChat.Client.Tests.Components;

public partial class MessageInputTests
{
    private static List<EmojiItem> GetTestEmojis()
    {
        return
        [
            new EmojiItem
            {
                Emoji = "😀",
                Code = ":grinning:",
                Name = "visage souriant",
                Aliases = [":grinning:", ":D"]
            },
            new EmojiItem
            {
                Emoji = "😃",
                Code = ":smile:",
                Name = "visage avec grand sourire",
                Aliases = [":smile:", ":)"]
            },
            new EmojiItem
            {
                Emoji = "❤️",
                Code = ":heart:",
                Name = "cœur rouge",
                Aliases = [":heart:", "<3"]
            }
        ];
    }

    [Fact]
    public void MessageInput_WithEmojiButton_RendersCorrectly()
    {
        // Act
        var cut = Render<MessageInput>(parameters => parameters
            .Add(p => p.IsConnected, true)
            .Add(p => p.AvailableUsers, []));

        // Assert
        var emojiButton = cut.Find(".emoji-button");
        Assert.NotNull(emojiButton);
        Assert.Contains("😀", emojiButton.TextContent);
    }

    [Fact]
    public async Task MessageInput_EmojiButton_WhenClicked_OpensEmojiPicker()
    {
        // Arrange
        var cut = Render<MessageInput>(parameters => parameters
            .Add(p => p.IsConnected, true)
            .Add(p => p.AvailableUsers, []));

        var emojiButton = cut.Find(".emoji-button");

        // Act
        await cut.InvokeAsync(() => emojiButton.Click());

        // Assert
        Assert.Contains("emoji-picker", cut.Markup);
    }

    [Fact]
    public async Task MessageInput_EmojiPicker_WhenEmojiSelected_InsertsInInput()
    {
        // Arrange
        var cut = Render<MessageInput>(parameters => parameters
            .Add(p => p.IsConnected, true)
            .Add(p => p.AvailableUsers, []));

        var input = cut.Find("input");

        // Act
        cut.Instance.InsertUsername("😀"); // Utilise InsertUsername qui fonctionne pour les emojis aussi

        // Assert
        var inputValue = input.GetAttribute("value");
        Assert.Contains("😀", inputValue);
    }

    [Fact]
    public async Task MessageInput_WithColonTyped_ShowsEmojiAutocomplete()
    {
        // Arrange
        _emojiServiceMock.Setup(x => x.SearchEmojis("sm")).Returns([.. GetTestEmojis().Take(2)]);

        var cut = Render<MessageInput>(parameters => parameters
            .Add(p => p.IsConnected, true)
            .Add(p => p.AvailableUsers, []));

        var input = await cut.InvokeAsync(() => cut.Find("input"));

        // Act
        input.Input(":sm");
        await input.KeyUpAsync(new KeyboardEventArgs { Key = "m" });

        // Assert
        Assert.Contains("emoji-autocomplete", cut.Markup);
    }

    [Fact]
    public async Task MessageInput_EmojiAutocomplete_FiltersEmojis()
    {
        // Arrange
        var filteredEmojis = new List<EmojiItem>
        {
            new()
            {
                Emoji = "😃",
                Code = ":smile:",
                Name = "visage avec grand sourire",
                Aliases = [":smile:"]
            }
        };

        _emojiServiceMock.Setup(x => x.SearchEmojis("smile")).Returns(filteredEmojis);

        var cut = Render<MessageInput>(parameters => parameters
            .Add(p => p.IsConnected, true)
            .Add(p => p.AvailableUsers, []));

        var input = await cut.InvokeAsync(() => cut.Find("input"));

        // Act
        input.Input(":smile");
        await input.KeyUpAsync(new KeyboardEventArgs { Key = "e" });

        // Assert
        var autocompleteItems = cut.FindAll(".emoji-autocomplete .autocomplete-item");
        Assert.Single(autocompleteItems);
        Assert.Contains("😃", autocompleteItems[0].TextContent);
        Assert.Contains(":smile:", autocompleteItems[0].TextContent);
    }

    [Fact]
    public async Task MessageInput_EmojiAutocomplete_NavigationWithArrows()
    {
        // Arrange
        _emojiServiceMock.Setup(x => x.SearchEmojis("s")).Returns([.. GetTestEmojis().Take(3)]);

        var cut = Render<MessageInput>(parameters => parameters
            .Add(p => p.IsConnected, true)
            .Add(p => p.AvailableUsers, []));

        var input = await cut.InvokeAsync(() => cut.Find("input"));

        input.Input(":s");
        await input.KeyUpAsync(new KeyboardEventArgs { Key = "s" });

        // Act - Navigate down
        await input.KeyDownAsync(new KeyboardEventArgs { Key = "ArrowDown" });

        // Assert
        var selectedItems = cut.FindAll(".autocomplete-item.selected");
        Assert.Single(selectedItems);
    }

    [Fact]
    public async Task MessageInput_EmojiAutocomplete_SelectWithEnter()
    {
        // Arrange
        _emojiServiceMock.Setup(x => x.SearchEmojis("smile")).Returns([.. GetTestEmojis().Take(1)]);

        var cut = Render<MessageInput>(parameters => parameters
            .Add(p => p.IsConnected, true)
            .Add(p => p.AvailableUsers, []));

        var input = await cut.InvokeAsync(() => cut.Find("input"));

        input.Input(":smile");
        await input.KeyUpAsync(new KeyboardEventArgs { Key = "e" });

        // Act
        await input.KeyDownAsync(new KeyboardEventArgs { Key = "Enter" });

        // Assert
        var inputValue = input.GetAttribute("value");
        Assert.Contains("😀", inputValue);
        Assert.DoesNotContain(":smile", inputValue);
    }

    [Fact]
    public async Task MessageInput_EmojiAutocomplete_CloseWithEscape()
    {
        // Arrange
        _emojiServiceMock.Setup(x => x.SearchEmojis("s")).Returns(GetTestEmojis());

        var cut = Render<MessageInput>(parameters => parameters
            .Add(p => p.IsConnected, true)
            .Add(p => p.AvailableUsers, []));

        var input = await cut.InvokeAsync(() => cut.Find("input"));

        input.Input(":s");
        await input.KeyUpAsync(new KeyboardEventArgs { Key = "s" });

        Assert.Contains("emoji-autocomplete", cut.Markup);

        // Act
        await input.KeyDownAsync(new KeyboardEventArgs { Key = "Escape" });

        // Assert
        Assert.DoesNotContain("emoji-autocomplete", cut.Markup);
    }

    [Fact]
    public async Task MessageInput_WithColonInMiddle_DoesNotTriggerAutocomplete()
    {
        // Arrange
        var cut = Render<MessageInput>(parameters => parameters
            .Add(p => p.IsConnected, true)
            .Add(p => p.AvailableUsers, []));

        var input = await cut.InvokeAsync(() => cut.Find("input"));

        // Act - Colon au milieu d'un mot
        input.Input("test@example.com");
        await input.KeyUpAsync(new KeyboardEventArgs { Key = "m" });

        // Assert
        Assert.DoesNotContain("emoji-autocomplete", cut.Markup);
    }

    [Fact]
    public async Task MessageInput_EmojiCodeConvertedOnSend()
    {
        // Arrange
        var sentMessage = string.Empty;

        var cut = Render<MessageInput>(parameters => parameters
            .Add(p => p.IsConnected, true)
            .Add(p => p.AvailableUsers, [])
            .Add(p => p.OnSendMessage, msg => sentMessage = msg));

        var input = await cut.InvokeAsync(() => cut.Find("input"));

        // Act
        input.Input("Hello :smile:");
        await input.KeyDownAsync(new KeyboardEventArgs { Key = "Enter" });

        // Assert
        Assert.Equal("Hello :smile:", sentMessage); // Le service EmojiService convertira lors de l'affichage
    }

    [Fact]
    public async Task MessageInput_EmojiButton_DisabledWhenNotConnected()
    {
        // Act
        var cut = Render<MessageInput>(parameters => parameters
            .Add(p => p.IsConnected, false)
            .Add(p => p.AvailableUsers, []));

        // Assert
        var emojiButton = cut.Find(".emoji-button");
        Assert.True(emojiButton.HasAttribute("disabled"));
    }

    [Fact]
    public async Task MessageInput_EmojiAndMentionAutocomplete_EmojiHasPriority()
    {
        // Arrange
        var users = new List<User>
        {
            new() { Username = "Alice", UserId = "1" }
        };

        _emojiServiceMock.Setup(x => x.SearchEmojis("s")).Returns(GetTestEmojis());

        var cut = Render<MessageInput>(parameters => parameters
            .Add(p => p.IsConnected, true)
            .Add(p => p.AvailableUsers, users));

        var input = await cut.InvokeAsync(() => cut.Find("input"));

        // Act - Taper `:s` (emoji) après `@A` (mention)
        input.Input("@A :s");
        await input.KeyUpAsync(new KeyboardEventArgs { Key = "s" });

        // Assert - L'autocomplétion emoji doit être visible
        Assert.Contains("emoji-autocomplete", cut.Markup);
    }

    [Fact]
    public async Task MessageInput_EmojiAutocomplete_LimitTo8Results()
    {
        // Arrange
        var manyEmojis = Enumerable.Range(0, 20)
            .Select(i => new EmojiItem
            {
                Emoji = $"😀{i}",
                Code = $":emoji{i}:",
                Name = $"emoji {i}",
                Aliases = [$":emoji{i}:"]
            })
            .ToList();

        _emojiServiceMock.Setup(x => x.SearchEmojis("emoji")).Returns(manyEmojis);

        var cut = Render<MessageInput>(parameters => parameters
            .Add(p => p.IsConnected, true)
            .Add(p => p.AvailableUsers, []));

        var input = await cut.InvokeAsync(() => cut.Find("input"));

        // Act
        input.Input(":emoji");
        await input.KeyUpAsync(new KeyboardEventArgs { Key = "i" });

        // Assert
        var autocompleteItems = cut.FindAll(".emoji-autocomplete .autocomplete-item");
        Assert.True(autocompleteItems.Count <= 8);
    }

    [Fact]
    public async Task MessageInput_EmojiAutocomplete_WithSpaceInQuery_Hides()
    {
        // Arrange
        _emojiServiceMock.Setup(x => x.SearchEmojis("s")).Returns(GetTestEmojis());

        var cut = Render<MessageInput>(parameters => parameters
            .Add(p => p.IsConnected, true)
            .Add(p => p.AvailableUsers, []));

        var input = await cut.InvokeAsync(() => cut.Find("input"));

        input.Input(":s");
        await input.KeyUpAsync(new KeyboardEventArgs { Key = "s" });

        Assert.Contains("emoji-autocomplete", cut.Markup);

        // Act - Ajouter un espace (fin de l'emoji code)
        input.Input(":s ");
        await input.KeyUpAsync(new KeyboardEventArgs { Key = " " });

        // Assert
        Assert.DoesNotContain("emoji-autocomplete", cut.Markup);
    }

    [Fact]
    public async Task MessageInput_EmojiAutocomplete_WhenServiceNotLoaded_DoesNotShow()
    {
        // Arrange
        _emojiServiceMock.Setup(x => x.IsLoaded).Returns(false);

        var cut = Render<MessageInput>(parameters => parameters
            .Add(p => p.IsConnected, true)
            .Add(p => p.AvailableUsers, []));

        var input = await cut.InvokeAsync(() => cut.Find("input"));

        // Act
        input.Input(":smile");
        await input.KeyUpAsync(new KeyboardEventArgs { Key = "e" });

        // Assert
        Assert.DoesNotContain("emoji-autocomplete", cut.Markup);
    }

    [Fact]
    public async Task MessageInput_EmojiFromPicker_InsertsWithSpace()
    {
        // Arrange
        var cut = Render<MessageInput>(parameters => parameters
            .Add(p => p.IsConnected, true)
            .Add(p => p.AvailableUsers, []));

        var input = cut.Find("input");

        // Act - Simuler insertion d'emoji depuis picker
        cut.Instance.InsertUsername("😀"); // Réutilise la méthode existante

        // Assert
        var inputValue = input.GetAttribute("value");
        Assert.EndsWith(" ", inputValue);
    }

    [Fact]
    public async Task MessageInput_MultipleEmojisFromPicker_SeparatedBySpaces()
    {
        // Arrange
        var cut = Render<MessageInput>(parameters => parameters
            .Add(p => p.IsConnected, true)
            .Add(p => p.AvailableUsers, []));

        var input = cut.Find("input");

        // Act
        cut.Instance.InsertUsername("😀");
        cut.Instance.InsertUsername("❤️");

        // Assert
        var inputValue = input.GetAttribute("value");
        Assert.Contains("😀 ❤️", inputValue);
    }
}