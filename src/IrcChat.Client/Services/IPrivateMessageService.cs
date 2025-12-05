using IrcChat.Shared.Models;

namespace IrcChat.Client.Services;

public interface IPrivateMessageService
{
    event Action<PrivateMessage>? OnPrivateMessageReceived;

    event Action<PrivateMessage>? OnPrivateMessageSent;

    event Action<string, List<Guid>>? OnMessagesRead;

    event Action? OnUnreadCountChanged;

    event Action<string>? OnConversationDeleted;

    void NotifyPrivateMessageReceived(PrivateMessage message);

    void NotifyPrivateMessageSent(PrivateMessage message);

    void NotifyMessagesRead(string username, List<Guid> messageIds);

    Task<List<PrivateConversation>> GetConversationsAsync(string userId);

    Task<List<PrivateMessage>> GetPrivateMessagesAsync(string userId, string otherUserId);

    Task<int> GetUnreadCountAsync(string userId);

    Task<bool> DeleteConversationAsync(string userId, string otherUserId);
}