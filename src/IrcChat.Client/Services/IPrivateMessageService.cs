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
    Task<List<PrivateConversation>> GetConversationsAsync(string username);
    Task<List<PrivateMessage>> GetPrivateMessagesAsync(string username, string otherUsername);
    Task<int> GetUnreadCountAsync(string username);
    Task<bool> DeleteConversationAsync(string username, string otherUsername);
}
