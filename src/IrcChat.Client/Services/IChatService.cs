using IrcChat.Shared.Models;

namespace IrcChat.Client.Services;

public interface IChatService : IAsyncDisposable
{
    event Action<Message>? OnMessageReceived;
    event Action<string, string>? OnUserJoined;
    event Action<string, string>? OnUserLeft;
    event Action<List<User>>? OnUserListUpdated;
    event Action<string, bool>? OnChannelMuteStatusChanged;
    event Action<string>? OnMessageBlocked;

    Task InitializeAsync(string? token = null);
    Task JoinChannel(string username, string channel);
    Task LeaveChannel(string channel);
    Task SendMessage(SendMessageRequest request);
    Task SendPrivateMessage(SendPrivateMessageRequest request);
    Task MarkPrivateMessagesAsRead(string senderUsername);
}