// src/IrcChat.Client/Services/PrivateMessageService.cs
using System.Diagnostics.CodeAnalysis;
using System.Net.Http.Json;
using IrcChat.Shared.Models;

namespace IrcChat.Client.Services;

public class PrivateMessageService(HttpClient httpClient, ILogger<PrivateMessageService> logger) : IPrivateMessageService
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

    public async Task<List<PrivateConversation>> GetConversationsAsync(string userId)
    {
        try
        {
            var result = await httpClient.GetFromJsonAsync<List<PrivateConversation>>(
                $"/api/private-messages/conversations/{userId}");
            return result ?? [];
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Erreur lors de la récupération des conversations pour {Username}", userId);
            return [];
        }
    }

    public async Task<List<PrivateMessage>> GetPrivateMessagesAsync(string userId, string otherUserId)
    {
        try
        {
            var result = await httpClient.GetFromJsonAsync<List<PrivateMessage>>(
                $"/api/private-messages/{userId}/with/{otherUserId}");
            return result ?? [];
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Erreur lors de la récupération des messages entre {Username} et {OtherUsername}",
                userId,
                otherUserId);
            return [];
        }
    }

    public async Task<int> GetUnreadCountAsync(string userId)
    {
        try
        {
            var result = await httpClient.GetFromJsonAsync<UnreadCountResponse>(
                $"/api/private-messages/{userId}/unread-count");
            return result?.UnreadCount ?? 0;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Erreur lors de la récupération du nombre de messages non lus pour {Username}", userId);
            return 0;
        }
    }

    public async Task<bool> DeleteConversationAsync(string userId, string otherUserId)
    {
        try
        {
            var response = await httpClient.DeleteAsync(
                $"/api/private-messages/{userId}/conversation/{otherUserId}");

            if (response.IsSuccessStatusCode)
            {
                OnConversationDeleted?.Invoke(otherUserId);
                OnUnreadCountChanged?.Invoke();
                return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Erreur lors de la suppression de la conversation entre {Username} et {OtherUsername}",
                userId,
                otherUserId);
            return false;
        }
    }

    // Suppression des warnings SonarQube pour les propriétés utilisées par la désérialisation JSON
    [SuppressMessage("Minor Code Smell", "S1144:Unused private types or members should be removed", Justification = "Utilisé par la désérialisation JSON")]
    [SuppressMessage("Minor Code Smell", "S3459:Unassigned members should be removed", Justification = "Propriété assignée par la désérialisation JSON")]
    private sealed class UnreadCountResponse
    {
        public int UnreadCount { get; init; }
    }
}