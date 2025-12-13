using IrcChat.Shared.Models;
using Microsoft.AspNetCore.SignalR.Client;

namespace IrcChat.Client.Services;

public interface IChatService : IAsyncDisposable
{
    event Action<Message>? OnMessageReceived;

    event Action<string, string, string>? OnUserJoined;

    event Action<string, string, string>? OnUserLeft;

    event Action<string, bool>? OnChannelMuteStatusChanged;

    event Action<string>? OnMessageBlocked;

    event Action<string>? OnChannelDeleted;

    event Action<string>? OnChannelNotFound;

    event Action? OnChannelListUpdated;

    event Action<string, bool>? OnUserStatusChanged;

    /// <summary>
    /// Événement levé quand un utilisateur est rendu mute dans un salon
    /// </summary>
    event Action<string, string, string, string, string> OnUserMuted;

    /// <summary>
    /// Événement levé quand un utilisateur reçoit la parole dans un salon
    /// </summary>
    event Action<string, string, string, string, string> OnUserUnmuted;

    /// <summary>
    /// Événement levé quand un message est supprimé dans un salon
    /// </summary>
    event Action<Guid, string>? OnMessageDeleted;

    // Événements pour l'état de la connexion SignalR
    event Action? OnDisconnected;

    event Action<string?>? OnReconnecting;

    event Action? OnReconnected;

    bool IsInitialized { get; }

    Task InitializeAsync(IHubConnectionBuilder hubConnectionBuilder);

    Task JoinChannel(string channel);

    Task LeaveChannel(string channel);

    Task SendMessage(SendMessageRequest request);

    Task SendPrivateMessage(SendPrivateMessageRequest request);

    Task MarkPrivateMessagesAsRead(string senderUserId);
}