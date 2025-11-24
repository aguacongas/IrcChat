using IrcChat.Shared.Models;
using Microsoft.AspNetCore.SignalR.Client;

namespace IrcChat.Client.Services;

public class ChatService(IPrivateMessageService privateMessageService,
    IUnifiedAuthService authService,
    ILogger<ChatService> logger) : IChatService
{
    private HubConnection? _hubConnection;
    private Timer? _pingTimer;

    // Events pour les canaux publics
    public event Action<Message>? OnMessageReceived;
    public event Action<string, string, string>? OnUserJoined;
    public event Action<string, string, string>? OnUserLeft;

    // Events pour le mute
    public event Action<string, bool>? OnChannelMuteStatusChanged;
    public event Action<string>? OnMessageBlocked;

    // Events pour la gestion des canaux
    public event Action<string>? OnChannelDeleted;
    public event Action<string>? OnChannelNotFound;
    public event Action? OnChannelListUpdated;

    // Event pour le statut de connexion des utilisateurs
    public event Action<string, bool>? OnUserStatusChanged;

    public event Action<string, string, string, string, string>? OnUserMuted;
    public event Action<string, string, string, string, string>? OnUserUnmuted;

    public bool IsInitialized => _hubConnection != null && _hubConnection.State == HubConnectionState.Connected;

    public async Task InitializeAsync(IHubConnectionBuilder hubConnectionBuilder)
    {
        _hubConnection = hubConnectionBuilder.Build();

        // Handlers pour les canaux publics
        _hubConnection.On<Message>("ReceiveMessage", message => OnMessageReceived?.Invoke(message));

        _hubConnection.On<string, string, string>("UserJoined", (username, userId, channel) => OnUserJoined?.Invoke(username, userId, channel));

        _hubConnection.On<string, string, string>("UserLeft", (username, userId, channel) => OnUserLeft?.Invoke(username, userId, channel));

        // Handlers pour le mute
        _hubConnection.On<string, bool>("ChannelMuteStatusChanged", (channel, isMuted) => OnChannelMuteStatusChanged?.Invoke(channel, isMuted));

        _hubConnection.On<string>("MessageBlocked", reason => OnMessageBlocked?.Invoke(reason));

        // Handlers pour la gestion des canaux
        _hubConnection.On<string>("ChannelDeleted", channelName => OnChannelDeleted?.Invoke(channelName));

        _hubConnection.On<string>("ChannelNotFound", channelName => OnChannelNotFound?.Invoke(channelName));

        _hubConnection.On("ChannelListUpdated", () => OnChannelListUpdated?.Invoke());

        // Handler pour le statut de connexion des utilisateurs
        _hubConnection.On<string, bool>("UserStatusChanged", (username, isOnline) => OnUserStatusChanged?.Invoke(username, isOnline));

        // Handlers pour les messages privés
        _hubConnection.On<PrivateMessage>("ReceivePrivateMessage", message => privateMessageService.NotifyPrivateMessageReceived(message));

        _hubConnection.On<PrivateMessage>("PrivateMessageSent", message => privateMessageService.NotifyPrivateMessageSent(message));

        _hubConnection.On<string, List<Guid>>("PrivateMessagesRead", (username, messageIds) => privateMessageService.NotifyMessagesRead(username, messageIds));

        // Handler pour le mute d'un utilisateur
        _hubConnection.On<string, string, string, string, string>(
            "UserMuted",
            (channel, userId, username, mutedByUserId, mutedByUsername)
            => OnUserMuted?.Invoke(channel, userId, username, mutedByUserId, mutedByUsername));

        // Handler pour le unmute d'un utilisateur
        _hubConnection.On<string, string, string, string, string>(
            "UserUnmuted",
            (channel, userId, username, unmutedByUserId, unmutedByUsername)
            => OnUserUnmuted?.Invoke(channel, userId, username, unmutedByUserId, unmutedByUsername));

        await _hubConnection.StartAsync();

        _pingTimer = new Timer(async _ =>
        {
            try
            {
                if (_hubConnection?.State == HubConnectionState.Connected)
                {
                    var userId = await authService.GetClientUserIdAsync();

                    if (!string.IsNullOrEmpty(userId))
                    {
                        await _hubConnection.SendAsync("Ping", authService.Username, userId);
                    }
                    else
                    {
                        logger.LogWarning("UserId vide lors du ping pour {Username}", authService.Username);
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Erreur lors de l'envoi du ping au serveur SignalR");
            }
        }, null, TimeSpan.Zero, TimeSpan.FromSeconds(30));
    }

    // Méthodes pour les canaux publics
    public async Task JoinChannel(string channel)
    {
        if (_hubConnection != null)
        {
            await _hubConnection.SendAsync("JoinChannel", channel);
        }
    }

    public async Task LeaveChannel(string channel)
    {
        if (_hubConnection != null)
        {
            await _hubConnection.SendAsync("LeaveChannel", channel);
        }
    }

    public async Task SendMessage(SendMessageRequest request)
    {
        if (_hubConnection != null)
        {
            await _hubConnection.SendAsync("SendMessage", request);
        }
    }

    // Méthodes pour les messages privés
    public async Task SendPrivateMessage(SendPrivateMessageRequest request)
    {
        if (_hubConnection != null)
        {
            await _hubConnection.SendAsync("SendPrivateMessage", request);
        }
    }

    public async Task MarkPrivateMessagesAsRead(string senderUserId)
    {
        if (_hubConnection != null)
        {
            await _hubConnection.SendAsync("MarkPrivateMessagesAsRead", senderUserId);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_pingTimer != null)
        {
            await _pingTimer.DisposeAsync();
        }

        if (_hubConnection != null)
        {
            await _hubConnection.DisposeAsync();
        }

        GC.SuppressFinalize(this);
    }
}