using IrcChat.Shared.Models;
using System.Net.Http.Json;

namespace IrcChat.Client.Services;

public class PrivateMessageService(HttpClient httpClient)
{
    public event Action<PrivateMessage>? OnPrivateMessageReceived;
    public event Action<PrivateMessage>? OnPrivateMessageSent;
    public event Action<string, List<Guid>>? OnMessagesRead;
    public event Action? OnUnreadCountChanged;

    public void NotifyPrivateMessageReceived(PrivateMessage message)
    {
        OnPrivateMessageReceived?.Invoke(message);
        OnUnreadCountChanged?.Invoke();
    }

    public void NotifyPrivateMessageSent(PrivateMessage message)
    {
        OnPrivateMessageSent?.Invoke(message);
    }

    public void NotifyMessagesRead(string username, List<Guid> messageIds)
    {
        OnMessagesRead?.Invoke(username, messageIds);
    }

    public async Task<List<PrivateConversation>> GetConversationsAsync(string username)
    {
        try
        {
            var result = await httpClient.GetFromJsonAsync<List<PrivateConversation>>(
                $"/api/private-messages/conversations/{username}");
            return result ?? [];
        }
        catch
        {
            return [];
        }
    }

    public async Task<List<PrivateMessage>> GetPrivateMessagesAsync(string username, string otherUsername)
    {
        try
        {
            var result = await httpClient.GetFromJsonAsync<List<PrivateMessage>>(
                $"/api/private-messages/{username}/with/{otherUsername}");
            return result ?? [];
        }
        catch
        {
            return [];
        }
    }

    public async Task<int> GetUnreadCountAsync(string username)
    {
        try
        {
            var result = await httpClient.GetFromJsonAsync<UnreadCountResponse>(
                $"/api/private-messages/{username}/unread-count");
            return result?.UnreadCount ?? 0;
        }
        catch
        {
            return 0;
        }
    }

    private class UnreadCountResponse
    {
        public int UnreadCount { get; set; }
    }
}