using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Options;
using IrcChat.Shared.Models;
using IrcChat.Client.Models;
using System.Threading;

namespace IrcChat.Client.Services;

public class ChatService(IOptions<ApiSettings> apiSettings) : IAsyncDisposable
{
    private HubConnection? _hubConnection;
    private readonly ApiSettings _apiSettings = apiSettings.Value;
    private Timer? _pingTimer;

    public event Action<Message>? OnMessageReceived;
    public event Action<string, string>? OnUserJoined;
    public event Action<string, string>? OnUserLeft;
    public event Action<List<User>>? OnUserListUpdated;

    public async Task InitializeAsync(string? token = null)
    {
        var hubUrl = !string.IsNullOrEmpty(_apiSettings.SignalRHubUrl)
            ? _apiSettings.SignalRHubUrl
            : $"{_apiSettings.BaseUrl}/chathub";

        _hubConnection = new HubConnectionBuilder()
            .WithUrl(hubUrl, options =>
            {
                if (!string.IsNullOrEmpty(token))
                {
                    options.AccessTokenProvider = () => Task.FromResult<string?>(token);
                }
            })
            .WithAutomaticReconnect()
            .Build();

        _hubConnection.On<Message>("ReceiveMessage", message =>
        {
            OnMessageReceived?.Invoke(message);
        });

        _hubConnection.On<string, string>("UserJoined", (username, channel) =>
        {
            OnUserJoined?.Invoke(username, channel);
        });

        _hubConnection.On<string, string>("UserLeft", (username, channel) =>
        {
            OnUserLeft?.Invoke(username, channel);
        });

        _hubConnection.On<List<User>>("UpdateUserList", users =>
        {
            OnUserListUpdated?.Invoke(users);
        });

        await _hubConnection.StartAsync();

        _pingTimer = new Timer(async _ =>
        {
            try
            {
                if (_hubConnection?.State == HubConnectionState.Connected)
                {
                    await _hubConnection.SendAsync("Ping");
                }
            }
            catch (Exception ex)
            {
                // Log error (assuming a logger is available)
                Console.Error.WriteLine($"Error sending ping: {ex.Message}");
            }
        }, null, TimeSpan.Zero, TimeSpan.FromSeconds(30));
    }

    public async Task JoinChannel(string username, string channel)
    {
        if (_hubConnection != null)
            await _hubConnection.SendAsync("JoinChannel", username, channel);
    }

    public async Task LeaveChannel(string channel)
    {
        if (_hubConnection != null)
            await _hubConnection.SendAsync("LeaveChannel", channel);
    }

    public async Task SendMessage(SendMessageRequest request)
    {
        if (_hubConnection != null)
            await _hubConnection.SendAsync("SendMessage", request);
    }

    public async ValueTask DisposeAsync()
    {
        if (_pingTimer != null)
        {
            await _pingTimer.DisposeAsync();
        }

        if (_hubConnection != null)
            await _hubConnection.DisposeAsync();
    }
}
