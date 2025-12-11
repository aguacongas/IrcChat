using IrcChat.Client.Components;
using IrcChat.Client.Models;
using IrcChat.Client.Services;
using Microsoft.Extensions.DependencyInjection;

namespace IrcChat.Client.Tests.Components;

public class EmojiPickerTests : BunitContext
{
    private readonly Mock<IEmojiService> _emojiServiceMock;

    public EmojiPickerTests()
    {
        _emojiServiceMock = new Mock<IEmojiService>();

        Services.AddSingleton(_emojiServiceMock.Object);
    }

    private static List<EmojiCategory> GetTestCategories()
    {
        return
        [
            new EmojiCategory
            {
                Id = "smileys-emotion",
                Name = "Smileys & Emotion",
                Icon = "😀",
                Count = 2,
                Order = 1
            },
            new EmojiCategory
            {
                Id = "symbols",
                Name = "Symbols",
                Icon = "🔣",
                Count = 1,
                Order = 8
            }
        ];
    }

    private static List<EmojiItem> GetTestEmojis()
    {
        return
        [
            new EmojiItem
            {
                Emoji = "😀",
                Code = ":grinning:",
                Name = "visage souriant",
                Category = "Smileys & Emotion"
            },
            new EmojiItem
            {
                Emoji = "😃",
                Code = ":smile:",
                Name = "visage avec grand sourire",
                Category = "Smileys & Emotion"
            },
            new EmojiItem
            {
                Emoji = "❤️",
                Code = ":heart:",
                Name = "cœur rouge",
                Category = "Symbols"
            }
        ];
    }

    [Fact]
    public void EmojiPicker_WhenClosed_NotVisible()
    {
        // Arrange
        _emojiServiceMock.Setup(x => x.GetCategories()).Returns(GetTestCategories());
        _emojiServiceMock.Setup(x => x.GetAllEmojis()).Returns(GetTestEmojis());

        // Act
        var cut = Render<EmojiPicker>(parameters => parameters
            .Add(p => p.IsOpen, false));

        // Assert
        Assert.DoesNotContain("emoji-picker-overlay", cut.Markup);
    }

    [Fact]
    public void EmojiPicker_WhenOpened_DisplaysEmojis()
    {
        // Arrange
        _emojiServiceMock.Setup(x => x.GetCategories()).Returns(GetTestCategories());
        _emojiServiceMock.Setup(x => x.GetAllEmojis()).Returns(GetTestEmojis());

        // Act
        var cut = Render<EmojiPicker>(parameters => parameters
            .Add(p => p.IsOpen, true));

        // Assert
        Assert.Contains("emoji-picker", cut.Markup);
        Assert.Contains("emoji-grid", cut.Markup);
    }

    [Fact]
    public async Task EmojiPicker_SearchInput_FiltersEmojis()
    {
        // Arrange
        var filteredEmojis = new List<EmojiItem>
        {
            new()
            {
                Emoji = "😀",
                Code = ":grinning:",
                Name = "visage souriant",
                Category = "Smileys & Emotion"
            }
        };

        _emojiServiceMock.Setup(x => x.GetCategories()).Returns(GetTestCategories());
        _emojiServiceMock.Setup(x => x.GetAllEmojis()).Returns(GetTestEmojis());
        _emojiServiceMock.Setup(x => x.SearchEmojis("grin")).Returns(filteredEmojis);

        var cut = Render<EmojiPicker>(parameters => parameters
            .Add(p => p.IsOpen, true));

        var searchInput = cut.Find(".emoji-search");

        // Act
        searchInput.Input("grin");
        await searchInput.KeyUpAsync();

        // Assert
        var emojiItems = cut.FindAll(".emoji-item");
        Assert.Single(emojiItems);
        Assert.Contains("😀", emojiItems[0].TextContent);
    }

    [Fact]
    public void EmojiPicker_CategoryButton_FiltersCategory()
    {
        // Arrange
        var categoryEmojis = new List<EmojiItem>
        {
            new()
            {
                Emoji = "❤️",
                Code = ":heart:",
                Name = "cœur rouge",
                Category = "Symbols"
            }
        };

        _emojiServiceMock.Setup(x => x.GetCategories()).Returns(GetTestCategories());
        _emojiServiceMock.Setup(x => x.GetAllEmojis()).Returns(GetTestEmojis());
        _emojiServiceMock.Setup(x => x.GetEmojisByCategory("symbols")).Returns(categoryEmojis);

        var cut = Render<EmojiPicker>(parameters => parameters
            .Add(p => p.IsOpen, true));

        var categoryButtons = cut.FindAll(".emoji-category-btn");
        var symbolsButton = categoryButtons.First(b => b.TextContent.Contains("🔣"));

        // Act
        symbolsButton.Click();

        // Assert
        var emojiItems = cut.FindAll(".emoji-item");
        Assert.Single(emojiItems);
        Assert.Contains("❤️", emojiItems[0].TextContent);
    }

    [Fact]
    public async Task EmojiPicker_EmojiClick_InvokesCallback()
    {
        // Arrange
        string? selectedEmoji = null;
        _emojiServiceMock.Setup(x => x.GetCategories()).Returns(GetTestCategories());
        _emojiServiceMock.Setup(x => x.GetAllEmojis()).Returns(GetTestEmojis());

        var cut = Render<EmojiPicker>(parameters => parameters
            .Add(p => p.IsOpen, true)
            .Add(p => p.OnEmojiSelected, emoji => selectedEmoji = emoji));

        var emojiItems = cut.FindAll(".emoji-item");

        // Act
        await cut.InvokeAsync(() => emojiItems[0].Click());

        // Assert
        Assert.NotNull(selectedEmoji);
    }

    [Fact]
    public void EmojiPicker_WithEmptySearch_ShowsAllEmojis()
    {
        // Arrange
        _emojiServiceMock.Setup(x => x.GetCategories()).Returns(GetTestCategories());
        _emojiServiceMock.Setup(x => x.GetAllEmojis()).Returns(GetTestEmojis());

        var cut = Render<EmojiPicker>(parameters => parameters
            .Add(p => p.IsOpen, true));

        // Assert
        var emojiItems = cut.FindAll(".emoji-item");
        Assert.Equal(3, emojiItems.Count);
    }

    [Fact]
    public void EmojiPicker_WithNoMatch_ShowsEmpty()
    {
        // Arrange
        _emojiServiceMock.Setup(x => x.GetCategories()).Returns(GetTestCategories());
        _emojiServiceMock.Setup(x => x.GetAllEmojis()).Returns(GetTestEmojis());
        _emojiServiceMock.Setup(x => x.SearchEmojis("xyz")).Returns([]);

        var cut = Render<EmojiPicker>(parameters => parameters
            .Add(p => p.IsOpen, true));

        var searchInput = cut.Find(".emoji-search");

        // Act
        searchInput.Input("xyz");
        searchInput.KeyUp();

        // Assert
        Assert.Contains("Aucun emoji trouvé", cut.Markup);
    }

    [Fact]
    public void EmojiPicker_DisplaysEmojiGrid_Correctly()
    {
        // Arrange
        _emojiServiceMock.Setup(x => x.GetCategories()).Returns(GetTestCategories());
        _emojiServiceMock.Setup(x => x.GetAllEmojis()).Returns(GetTestEmojis());

        // Act
        var cut = Render<EmojiPicker>(parameters => parameters
            .Add(p => p.IsOpen, true));

        // Assert
        var grid = cut.Find(".emoji-grid");
        Assert.NotNull(grid);

        var emojiItems = cut.FindAll(".emoji-item");
        Assert.NotEmpty(emojiItems);
    }

    [Fact]
    public void EmojiPicker_AllCategories_Available()
    {
        // Arrange
        _emojiServiceMock.Setup(x => x.GetCategories()).Returns(GetTestCategories());
        _emojiServiceMock.Setup(x => x.GetAllEmojis()).Returns(GetTestEmojis());

        // Act
        var cut = Render<EmojiPicker>(parameters => parameters
            .Add(p => p.IsOpen, true));

        // Assert
        var categoryButtons = cut.FindAll(".emoji-category-btn");

        // +1 pour le bouton "Tous" (🔍)
        Assert.Equal(GetTestCategories().Count + 1, categoryButtons.Count);
    }

    [Fact]
    public async Task EmojiPicker_CloseButton_ClosesPicke()
    {
        // Arrange
        var isOpen = true;
        _emojiServiceMock.Setup(x => x.GetCategories()).Returns(GetTestCategories());
        _emojiServiceMock.Setup(x => x.GetAllEmojis()).Returns(GetTestEmojis());

        var cut = Render<EmojiPicker>(parameters => parameters
            .Add(p => p.IsOpen, isOpen)
            .Add(p => p.IsOpenChanged, newValue => isOpen = newValue));

        var closeButton = cut.Find(".emoji-close");

        // Act
        await cut.InvokeAsync(() => closeButton.Click());

        // Assert
        Assert.False(isOpen);
    }

    [Fact]
    public async Task EmojiPicker_OverlayClick_ClosesPicke()
    {
        // Arrange
        var isOpen = true;
        _emojiServiceMock.Setup(x => x.GetCategories()).Returns(GetTestCategories());
        _emojiServiceMock.Setup(x => x.GetAllEmojis()).Returns(GetTestEmojis());

        var cut = Render<EmojiPicker>(parameters => parameters
            .Add(p => p.IsOpen, isOpen)
            .Add(p => p.IsOpenChanged, newValue => isOpen = newValue));

        var overlay = cut.Find(".emoji-picker-overlay");

        // Act
        await cut.InvokeAsync(() => overlay.Click());

        // Assert
        Assert.False(isOpen);
    }

    [Fact]
    public void EmojiPicker_CategorySelection_HighlightsActive()
    {
        // Arrange
        _emojiServiceMock.Setup(x => x.GetCategories()).Returns(GetTestCategories());
        _emojiServiceMock.Setup(x => x.GetAllEmojis()).Returns(GetTestEmojis());
        _emojiServiceMock.Setup(x => x.GetEmojisByCategory(It.IsAny<string>())).Returns(GetTestEmojis());

        var cut = Render<EmojiPicker>(parameters => parameters
            .Add(p => p.IsOpen, true));

        var categoryButtons = cut.FindAll(".emoji-category-btn");
        var symbolsButton = categoryButtons.First(b => b.TextContent.Contains("🔣"));

        // Act
        symbolsButton.Click();

        // Assert
        var activeButtons = cut.FindAll(".emoji-category-btn.active");
        Assert.Single(activeButtons);
        Assert.Contains("🔣", activeButtons[0].TextContent);
    }

    [Fact]
    public void EmojiPicker_InitialState_ShowsAllCategory()
    {
        // Arrange
        _emojiServiceMock.Setup(x => x.GetCategories()).Returns(GetTestCategories());
        _emojiServiceMock.Setup(x => x.GetAllEmojis()).Returns(GetTestEmojis());

        // Act
        var cut = Render<EmojiPicker>(parameters => parameters
            .Add(p => p.IsOpen, true));

        // Assert
        var allButton = cut.Find(".emoji-category-btn.active");
        Assert.Contains("🔍", allButton.TextContent);
    }

    [Fact]
    public void EmojiPicker_SearchAndCategory_SearchTakesPrecedence()
    {
        // Arrange
        var searchResults = new List<EmojiItem>
        {
            new()
            {
                Emoji = "😀",
                Code = ":grinning:",
                Name = "visage souriant",
                Category = "Smileys & Emotion"
            }
        };

        _emojiServiceMock.Setup(x => x.GetCategories()).Returns(GetTestCategories());
        _emojiServiceMock.Setup(x => x.GetAllEmojis()).Returns(GetTestEmojis());
        _emojiServiceMock.Setup(x => x.SearchEmojis("grin")).Returns(searchResults);

        var cut = Render<EmojiPicker>(parameters => parameters
            .Add(p => p.IsOpen, true));

        var searchInput = cut.Find(".emoji-search");

        // Act
        searchInput.Input("grin");
        searchInput.KeyUp();

        // Assert
        var emojiItems = cut.FindAll(".emoji-item");
        Assert.Single(emojiItems);
    }
}