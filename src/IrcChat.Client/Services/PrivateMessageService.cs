// src/IrcChat.Client/Services/PrivateMessageService.cs
using System.Net.Http.Json;
using IrcChat.Shared.Models;

namespace IrcChat.Client.Services;

public class PrivateMessageService(HttpClient httpClient) : IPrivateMessageService
{
    public event Action<PrivateMessage>? OnPrivateMessageReceived;
    public event Action<PrivateMessage>? OnPrivateMessageSent;
    public event Action<string, List<Guid>>? OnMessagesRead;
    public event Action? OnUnreadCountChanged;
    public event Action<string>? OnConversationDeleted;

    public void NotifyPrivateMessageReceived(PrivateMessage message)
    {
        OnPrivateMessageReceived?.Invoke(message);
        OnUnreadCountChanged?.Invoke();
    }

    public void NotifyPrivateMessageSent(PrivateMessage message) => OnPrivateMessageSent?.Invoke(message);

    public void NotifyMessagesRead(string username, List<Guid> messageIds) => OnMessagesRead?.Invoke(username, messageIds);

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

    public async Task<bool> DeleteConversationAsync(string username, string otherUsername)
    {
        try
        {
            var response = await httpClient.DeleteAsync(
                $"/api/private-messages/{username}/conversation/{otherUsername}");

            if (response.IsSuccessStatusCode)
            {
                OnConversationDeleted?.Invoke(otherUsername);
                OnUnreadCountChanged?.Invoke();
                return true;
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    private sealed class UnreadCountResponse
    {
        public int UnreadCount { get; init; }
    }
}