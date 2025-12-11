using IrcChat.Client.Models;
using IrcChat.Client.Services;
using Microsoft.Extensions.Logging;
using RichardSzalay.MockHttp;

namespace IrcChat.Client.Tests.Services;

public class EmojiServiceTests
{
    private readonly MockHttpMessageHandler _mockHttp;
    private readonly HttpClient _httpClient;
    private readonly Mock<ILogger<EmojiService>> _loggerMock;
    private readonly EmojiService _emojiService;

    public EmojiServiceTests()
    {
        _mockHttp = new MockHttpMessageHandler();
        _httpClient = _mockHttp.ToHttpClient();
        _httpClient.BaseAddress = new Uri("http://localhost/");
        _loggerMock = new Mock<ILogger<EmojiService>>();
        _emojiService = new EmojiService(_httpClient, _loggerMock.Object);
    }

    private static EmojiData CreateTestEmojiData()
    {
        return new EmojiData
        {
            Version = "15.1",
            GeneratedAt = DateTime.UtcNow,
            Emojis =
            [
                new EmojiItem
                {
                    Emoji = "😀",
                    Code = ":grinning:",
                    Name = "visage souriant",
                    NameEn = "grinning face",
                    Category = "Smileys & Emotion",
                    Keywords = ["visage", "sourire", "content"],
                    Aliases = [":grinning:", ":D", ":-D"],
                    Unicode = "U+1F600",
                    Version = "1.0"
                },
                new EmojiItem
                {
                    Emoji = "😃",
                    Code = ":smile:",
                    Name = "visage avec grand sourire",
                    NameEn = "grinning face with big eyes",
                    Category = "Smileys & Emotion",
                    Keywords = ["visage", "sourire", "heureux"],
                    Aliases = [":smile:", ":)", ":-)"],
                    Unicode = "U+1F603",
                    Version = "1.0"
                },
                new EmojiItem
                {
                    Emoji = "❤️",
                    Code = ":heart:",
                    Name = "cœur rouge",
                    NameEn = "red heart",
                    Category = "Symbols",
                    Keywords = ["cœur", "amour", "rouge"],
                    Aliases = [":heart:", "<3"],
                    Unicode = "U+2764",
                    Version = "1.0"
                },
                new EmojiItem
                {
                    Emoji = "👍",
                    Code = ":thumbs_up:",
                    Name = "pouce levé",
                    NameEn = "thumbs up",
                    Category = "People & Body",
                    Keywords = ["pouce", "bien", "ok"],
                    Aliases = [":thumbs_up:", "+1"],
                    Unicode = "U+1F44D",
                    Version = "1.0"
                }
            ],
            Categories =
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
                    Id = "people-body",
                    Name = "People & Body",
                    Icon = "👋",
                    Count = 1,
                    Order = 2
                },
                new EmojiCategory
                {
                    Id = "symbols",
                    Name = "Symbols",
                    Icon = "🔣",
                    Count = 1,
                    Order = 8
                }
            ]
        };
    }

    [Fact]
    public async Task InitializeAsync_LoadsEmojiData_Successfully()
    {
        // Arrange
        var emojiData = CreateTestEmojiData();
        _mockHttp.When("*/data/emojis.json")
            .Respond("application/json", System.Text.Json.JsonSerializer.Serialize(emojiData));

        // Act
        await _emojiService.InitializeAsync();

        // Assert
        Assert.True(_emojiService.IsLoaded);
        Assert.Equal(4, _emojiService.GetAllEmojis().Count);
        Assert.Equal(3, _emojiService.GetCategories().Count);
    }

    [Fact]
    public async Task InitializeAsync_WhenJsonInvalid_ThrowsException()
    {
        // Arrange
        _mockHttp.When("*/data/emojis.json")
            .Respond("application/json", "invalid json");

        // Act & Assert
        await Assert.ThrowsAsync<System.Text.Json.JsonException>(() => _emojiService.InitializeAsync());
    }

    [Fact]
    public async Task InitializeAsync_WhenEmptyData_ThrowsException()
    {
        // Arrange
        var emptyData = new EmojiData { Emojis = [], Categories = [] };
        _mockHttp.When("*/data/emojis.json")
            .Respond("application/json", System.Text.Json.JsonSerializer.Serialize(emptyData));

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => _emojiService.InitializeAsync());
    }

    [Fact]
    public async Task SearchEmojis_WithQuery_ReturnsMatches()
    {
        // Arrange
        var emojiData = CreateTestEmojiData();
        _mockHttp.When("*/data/emojis.json")
            .Respond("application/json", System.Text.Json.JsonSerializer.Serialize(emojiData));
        await _emojiService.InitializeAsync();

        // Act
        var results = _emojiService.SearchEmojis("smile");

        // Assert
        Assert.NotEmpty(results);
        Assert.Contains(results, e => e.Code == ":smile:");
    }

    [Fact]
    public async Task SearchEmojis_WithEmptyQuery_ReturnsEmpty()
    {
        // Arrange
        var emojiData = CreateTestEmojiData();
        _mockHttp.When("*/data/emojis.json")
            .Respond("application/json", System.Text.Json.JsonSerializer.Serialize(emojiData));
        await _emojiService.InitializeAsync();

        // Act
        var results = _emojiService.SearchEmojis(string.Empty);

        // Assert
        Assert.Empty(results);
    }

    [Fact]
    public async Task SearchEmojis_WithFrenchKeyword_FindsMatches()
    {
        // Arrange
        var emojiData = CreateTestEmojiData();
        _mockHttp.When("*/data/emojis.json")
            .Respond("application/json", System.Text.Json.JsonSerializer.Serialize(emojiData));
        await _emojiService.InitializeAsync();

        // Act
        var results = _emojiService.SearchEmojis("cœur");

        // Assert
        Assert.NotEmpty(results);
        Assert.Contains(results, e => e.Emoji == "❤️");
    }

    [Fact]
    public async Task SearchEmojis_WithEnglishKeyword_FindsMatches()
    {
        // Arrange
        var emojiData = CreateTestEmojiData();
        _mockHttp.When("*/data/emojis.json")
            .Respond("application/json", System.Text.Json.JsonSerializer.Serialize(emojiData));
        await _emojiService.InitializeAsync();

        // Act
        var results = _emojiService.SearchEmojis("heart");

        // Assert
        Assert.NotEmpty(results);
        Assert.Contains(results, e => e.Emoji == "❤️");
    }

    [Fact]
    public async Task GetEmojisByCategory_ReturnsCorrectEmojis()
    {
        // Arrange
        var emojiData = CreateTestEmojiData();
        _mockHttp.When("*/data/emojis.json")
            .Respond("application/json", System.Text.Json.JsonSerializer.Serialize(emojiData));
        await _emojiService.InitializeAsync();

        // Act
        var results = _emojiService.GetEmojisByCategory("smileys-emotion");

        // Assert
        Assert.Equal(2, results.Count);
        Assert.All(results, e => Assert.Equal("Smileys & Emotion", e.Category));
    }

    [Fact]
    public async Task GetEmojisByCategory_InvalidCategory_ReturnsEmpty()
    {
        // Arrange
        var emojiData = CreateTestEmojiData();
        _mockHttp.When("*/data/emojis.json")
            .Respond("application/json", System.Text.Json.JsonSerializer.Serialize(emojiData));
        await _emojiService.InitializeAsync();

        // Act
        var results = _emojiService.GetEmojisByCategory("unknown");

        // Assert
        Assert.Empty(results);
    }

    [Fact]
    public async Task GetCategories_ReturnsAllCategories()
    {
        // Arrange
        var emojiData = CreateTestEmojiData();
        _mockHttp.When("*/data/emojis.json")
            .Respond("application/json", System.Text.Json.JsonSerializer.Serialize(emojiData));
        await _emojiService.InitializeAsync();

        // Act
        var categories = _emojiService.GetCategories();

        // Assert
        Assert.Equal(3, categories.Count);
        Assert.Contains(categories, c => c.Name == "Smileys & Emotion");
    }

    [Fact]
    public async Task GetAllEmojis_ReturnsCompleteList()
    {
        // Arrange
        var emojiData = CreateTestEmojiData();
        _mockHttp.When("*/data/emojis.json")
            .Respond("application/json", System.Text.Json.JsonSerializer.Serialize(emojiData));
        await _emojiService.InitializeAsync();

        // Act
        var emojis = _emojiService.GetAllEmojis();

        // Assert
        Assert.Equal(4, emojis.Count);
    }

    [Fact]
    public void SearchEmojis_BeforeInitialize_ReturnsEmpty()
    {
        // Act
        var results = _emojiService.SearchEmojis("test");

        // Assert
        Assert.Empty(results);
    }

    [Fact]
    public void GetCategories_BeforeInitialize_ReturnsEmpty()
    {
        // Act
        var categories = _emojiService.GetCategories();

        // Assert
        Assert.Empty(categories);
    }

    [Fact]
    public void GetAllEmojis_BeforeInitialize_ReturnsEmpty()
    {
        // Act
        var emojis = _emojiService.GetAllEmojis();

        // Assert
        Assert.Empty(emojis);
    }
}