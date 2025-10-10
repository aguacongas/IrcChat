using Microsoft.AspNetCore.SignalR.Client;
using IrcChat.Shared.Models;

namespace IrcChat.Client.Services;

public class ChatService : IAsyncDisposable
{
    private HubConnection? _hubConnection;

    public event Action<Message>? OnMessageReceived;
    public event Action<string, string>? OnUserJoined;
    public event Action<string, string>? OnUserLeft;
    public event Action<List<User>>? OnUserListUpdated;

    public async Task InitializeAsync(string? token = null)
    {
        var url = "https://localhost:7000/chathub";

        _hubConnection = new HubConnectionBuilder()
            .WithUrl(url, options =>
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
    }

    public async Task JoinChannel(string username, string channel)
    {
        if (_hubConnection != null)
        {
            await _hubConnection.SendAsync("JoinChannel", username, channel);
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

    public async ValueTask DisposeAsync()
    {
        if (_hubConnection != null)
        {
            await _hubConnection.DisposeAsync();
        }
        GC.SuppressFinalize(this);
    }
}