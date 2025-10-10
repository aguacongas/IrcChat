using Microsoft.AspNetCore.SignalR;
using IrcChat.Shared.Models;
using IrcChat.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace IrcChat.Api.Hubs;

public class ChatHub(ChatDbContext db) : Hub
{
    private static readonly Dictionary<string, User> _connectedUsers = [];

    public async Task JoinChannel(string username, string channel)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, channel);

        var user = new User
        {
            Username = username,
            ConnectedAt = DateTime.UtcNow,
            ConnectionId = Context.ConnectionId
        };

        _connectedUsers[Context.ConnectionId] = user;

        await Clients.Group(channel).SendAsync("UserJoined", username, channel);
        await Clients.Group(channel).SendAsync("UpdateUserList", GetChannelUsers(channel));
    }

    public async Task LeaveChannel(string channel)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, channel);

        if (_connectedUsers.TryGetValue(Context.ConnectionId, out var user))
        {
            await Clients.Group(channel).SendAsync("UserLeft", user.Username, channel);
            await Clients.Group(channel).SendAsync("UpdateUserList", GetChannelUsers(channel));
        }
    }

    public async Task SendMessage(SendMessageRequest request)
    {
        var message = new Message
        {
            Username = request.Username,
            Content = request.Content,
            Channel = request.Channel,
            Timestamp = DateTime.UtcNow,
            IsDeleted = false
        };

        db.Messages.Add(message);
        await db.SaveChangesAsync();

        await Clients.Group(request.Channel).SendAsync("ReceiveMessage", message);
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (_connectedUsers.TryGetValue(Context.ConnectionId, out var user))
        {
            _connectedUsers.Remove(Context.ConnectionId);

            // Notifier tous les groupes auxquels l'utilisateur appartenait
            var channels = await db.Channels.Select(c => c.Name).ToListAsync();
            foreach (var channel in channels)
            {
                await Clients.Group(channel).SendAsync("UserLeft", user.Username, channel);
                await Clients.Group(channel).SendAsync("UpdateUserList", GetChannelUsers(channel));
            }
        }

        await base.OnDisconnectedAsync(exception);
    }

    private List<User> GetChannelUsers(string channel)
    {
        return _connectedUsers.Values.ToList();
    }
}