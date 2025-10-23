using Microsoft.AspNetCore.SignalR;
using IrcChat.Shared.Models;
using IrcChat.Api.Data;
using Microsoft.EntityFrameworkCore;
using IrcChat.Api.Services;

namespace IrcChat.Api.Hubs;

public class ChatHub(ChatDbContext db, ConnectionManagerService connectionManager) : Hub
{
    public async Task JoinChannel(string username, string channel)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, channel);

        var existingUser = await db.ConnectedUsers
            .FirstOrDefaultAsync(u => u.Username == username && u.Channel == channel);

        if (existingUser != null)
        {
            existingUser.ConnectionId = Context.ConnectionId;
            existingUser.LastPing = DateTime.UtcNow;
            existingUser.ServerInstanceId = connectionManager.GetInstanceId();
        }
        else
        {
            db.ConnectedUsers.Add(new ConnectedUser
            {
                Username = username,
                Channel = channel,
                ConnectionId = Context.ConnectionId,
                LastPing = DateTime.UtcNow,
                ServerInstanceId = connectionManager.GetInstanceId()
            });
        }

        await db.SaveChangesAsync();

        // Notifier les autres utilisateurs
        await Clients.Group(channel).SendAsync("UserJoined", username, channel);

        // Envoyer la liste mise à jour des utilisateurs
        var channelUsers = await GetChannelUsers(channel);
        await Clients.Group(channel).SendAsync("UpdateUserList", channelUsers);
    }

    public async Task LeaveChannel(string channel)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, channel);

        var connectedUser = await db.ConnectedUsers
            .FirstOrDefaultAsync(u => u.ConnectionId == Context.ConnectionId && u.Channel == channel);

        if (connectedUser != null)
        {
            db.ConnectedUsers.Remove(connectedUser);
            await db.SaveChangesAsync();

            await Clients.Group(channel).SendAsync("UserLeft", connectedUser.Username, channel);

            var channelUsers = await GetChannelUsers(channel);
            await Clients.Group(channel).SendAsync("UpdateUserList", channelUsers);
        }
    }

    public async Task SendMessage(SendMessageRequest request)
    {
        // Mettre à jour la dernière activité
        var connectedUser = await db.ConnectedUsers
            .FirstOrDefaultAsync(u => u.ConnectionId == Context.ConnectionId && u.Channel == request.Channel);

        if (connectedUser != null)
        {
            connectedUser.LastActivity = DateTime.UtcNow;
        }

        var message = new Message
        {
            Id = Guid.NewGuid(),
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

    public async Task Ping()
    {
        var user = await db.ConnectedUsers
            .FirstOrDefaultAsync(u => u.ConnectionId == Context.ConnectionId);
            
        if (user != null)
        {
            user.LastPing = DateTime.UtcNow;
            await db.SaveChangesAsync();
        }
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        // Récupérer toutes les connexions de cet utilisateur
        var userConnections = await db.ConnectedUsers
            .Where(u => u.ConnectionId == Context.ConnectionId)
            .ToListAsync();

        if (userConnections.Any())
        {
            // Notifier chaque canal
            foreach (var connection in userConnections)
            {
                await Clients.Group(connection.Channel)
                    .SendAsync("UserLeft", connection.Username, connection.Channel);

                db.ConnectedUsers.Remove(connection);

                // Envoyer la liste mise à jour
                var channelUsers = await GetChannelUsers(connection.Channel);
                await Clients.Group(connection.Channel)
                    .SendAsync("UpdateUserList", channelUsers);
            }

            await db.SaveChangesAsync();
        }

        await base.OnDisconnectedAsync(exception);
    }

    private async Task<List<User>> GetChannelUsers(string channel)
    {
        return await db.ConnectedUsers
            .Where(u => u.Channel == channel)
            .OrderBy(u => u.Username)
            .Select(u => new User
            {
                Username = u.Username,
                ConnectedAt = u.ConnectedAt,
                ConnectionId = u.ConnectionId
            })
            .ToListAsync();
    }
}
