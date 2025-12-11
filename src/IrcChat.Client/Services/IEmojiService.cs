using IrcChat.Client.Models;

namespace IrcChat.Client.Services;

public interface IEmojiService
{
    bool IsLoaded { get; }

    List<EmojiItem> GetAllEmojis();
    List<EmojiCategory> GetCategories();
    List<EmojiItem> GetEmojisByCategory(string categoryId);
    Task InitializeAsync();
    List<EmojiItem> SearchEmojis(string query);
}