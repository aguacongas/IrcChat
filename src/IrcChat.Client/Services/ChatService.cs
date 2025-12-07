using IrcChat.Shared.Models;
using Microsoft.AspNetCore.SignalR.Client;

namespace IrcChat.Client.Services;

public class ChatService(IPrivateMessageService privateMessageService,
    IUnifiedAuthService authService,
    IRequestAuthenticationService requestAuthService,
    ILogger<ChatService> logger) : IChatService
{
    private HubConnection? hubConnection;
    private IHubConnectionEvents? hubConnectionEvents;
    private Timer? pingTimer;

    public event Action<Message>? OnMessageReceived;

    public event Action<string, string, string>? OnUserJoined;

    public event Action<string, string, string>? OnUserLeft;

    public event Action<string, bool>? OnChannelMuteStatusChanged;

    public event Action<string>? OnMessageBlocked;

    public event Action<string>? OnChannelDeleted;

    public event Action<string>? OnChannelNotFound;

    public event Action? OnChannelListUpdated;

    public event Action<string, bool>? OnUserStatusChanged;

    public event Action<string, string, string, string, string>? OnUserMuted;

    public event Action<string, string, string, string, string>? OnUserUnmuted;

    public event Action? OnDisconnected;

    public event Action<string?>? OnReconnecting;

    public event Action? OnReconnected;

    public bool IsInitialized => hubConnection != null && hubConnection.State == HubConnectionState.Connected;

    public Func<HubConnection, IHubConnectionEvents> WrapConnectionEvents { get; set; } = connection => new HubConnectionEventWrapper(connection);

    public async Task InitializeAsync(IHubConnectionBuilder hubConnectionBuilder)
    {
        hubConnection = hubConnectionBuilder.Build();

        hubConnection.On<Message>("ReceiveMessage", message => OnMessageReceived?.Invoke(message));

        hubConnection.On<string, string, string>("UserJoined", (username, userId, channel) => OnUserJoined?.Invoke(username, userId, channel));

        hubConnection.On<string, string, string>("UserLeft", (username, userId, channel) => OnUserLeft?.Invoke(username, userId, channel));

        hubConnection.On<string, bool>("ChannelMuteStatusChanged", (channel, isMuted) => OnChannelMuteStatusChanged?.Invoke(channel, isMuted));

        hubConnection.On<string>("MessageBlocked", reason => OnMessageBlocked?.Invoke(reason));

        hubConnection.On<string>("ChannelDeleted", channelName => OnChannelDeleted?.Invoke(channelName));

        hubConnection.On<string>("ChannelNotFound", channelName => OnChannelNotFound?.Invoke(channelName));

        hubConnection.On("ChannelListUpdated", () => OnChannelListUpdated?.Invoke());

        hubConnection.On<string, bool>("UserStatusChanged", (username, isOnline) => OnUserStatusChanged?.Invoke(username, isOnline));

        hubConnection.On<PrivateMessage>("ReceivePrivateMessage", message => privateMessageService.NotifyPrivateMessageReceived(message));

        hubConnection.On<PrivateMessage>("PrivateMessageSent", message => privateMessageService.NotifyPrivateMessageSent(message));

        hubConnection.On<string, List<Guid>>("PrivateMessagesRead", (username, messageIds) => privateMessageService.NotifyMessagesRead(username, messageIds));

        hubConnection.On<string, string, string, string, string>(
            "UserMuted",
            (channel, userId, username, mutedByUserId, mutedByUsername)
            => OnUserMuted?.Invoke(channel, userId, username, mutedByUserId, mutedByUsername));

        hubConnection.On<string, string, string, string, string>(
            "UserUnmuted",
            (channel, userId, username, unmutedByUserId, unmutedByUsername)
            => OnUserUnmuted?.Invoke(channel, userId, username, unmutedByUserId, unmutedByUsername));

        hubConnectionEvents = WrapConnectionEvents(hubConnection);

        hubConnectionEvents.Closed += OnConnectionClosed;
        hubConnectionEvents.Reconnecting += OnConnectionReconnecting;
        hubConnectionEvents.Reconnected += OnConnectionReconnected;

        await hubConnection.StartAsync();
        requestAuthService.ConnectionId = hubConnection.ConnectionId;
        CreatePingTimer();
    }

    public async Task JoinChannel(string channel)
    {
        if (hubConnection != null)
        {
            await hubConnection.SendAsync("JoinChannel", channel);
        }
    }

    public async Task LeaveChannel(string channel)
    {
        if (hubConnection != null)
        {
            await hubConnection.SendAsync("LeaveChannel", channel);
        }
    }

    public async Task SendMessage(SendMessageRequest request)
    {
        if (hubConnection != null)
        {
            await hubConnection.SendAsync("SendMessage", request);
        }
    }

    public async Task SendPrivateMessage(SendPrivateMessageRequest request)
    {
        if (hubConnection != null)
        {
            await hubConnection.SendAsync("SendPrivateMessage", request);
        }
    }

    public async Task MarkPrivateMessagesAsRead(string senderUserId)
    {
        if (hubConnection != null)
        {
            await hubConnection.SendAsync("MarkPrivateMessagesAsRead", senderUserId);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (pingTimer != null)
        {
            await pingTimer.DisposeAsync();
        }

        if (hubConnection != null)
        {
            hubConnectionEvents!.Closed -= OnConnectionClosed;
            hubConnectionEvents!.Reconnecting -= OnConnectionReconnecting;
            hubConnectionEvents!.Reconnected -= OnConnectionReconnected;

            await hubConnection.DisposeAsync();
        }

        GC.SuppressFinalize(this);
    }

    private Task OnConnectionClosed(Exception? error)
    {
        logger.LogWarning(error, "Connexion SignalR fermée");
        pingTimer?.Dispose();
        pingTimer = null;
        OnDisconnected?.Invoke();
        return Task.CompletedTask;
    }

    private Task OnConnectionReconnecting(Exception? error)
    {
        logger.LogInformation(error, "Tentative de reconnexion SignalR...");
        OnReconnecting?.Invoke(error?.Message);
        return Task.CompletedTask;
    }

    private Task OnConnectionReconnected(string? connectionId)
    {
        logger.LogInformation("Reconnexion SignalR réussie (ConnectionId: {ConnectionId})", connectionId);
        requestAuthService.ConnectionId = connectionId;
        CreatePingTimer();
        OnReconnected?.Invoke();
        return Task.CompletedTask;
    }

    private void CreatePingTimer()
    {
        pingTimer = new Timer(
            async _ =>
            {
                try
                {
                    if (hubConnection?.State == HubConnectionState.Connected)
                    {
                        var userId = await authService.GetClientUserIdAsync();

                        if (!string.IsNullOrEmpty(userId))
                        {
                            await hubConnection.SendAsync("Ping", authService.Username, userId, authService.IsNoPvMode);
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
            },
            null,
            TimeSpan.Zero,
            TimeSpan.FromSeconds(30));
    }
}