using System.Security.Claims;
using IrcChat.Api.Data;
using IrcChat.Api.Hubs;
using IrcChat.Api.Services;
using IrcChat.Shared.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace IrcChat.Api.Tests.Hubs;

public class ChatHubTests : IAsyncDisposable
{
    private readonly ChatDbContext _db;
    private readonly Mock<IHubCallerClients> _clientsMock;
    private readonly Mock<ISingleClientProxy> _callerMock;
    private readonly Mock<IClientProxy> _allClientsMock;
    private readonly Mock<IClientProxy> _groupMock;
    private readonly Mock<ISingleClientProxy> _singleClientMock;
    private readonly Mock<HubCallerContext> _contextMock;
    private readonly Mock<IGroupManager> _groupManagerMock;
    private readonly Mock<ILogger<ChatHub>> _loggerMock;
    private readonly ChatHub _hub;
    private readonly string _testConnectionId = "test-connection-id";

    public ChatHubTests()
    {
        var options = new DbContextOptionsBuilder<ChatDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _db = new ChatDbContext(options);

        var connectionManagerOptions = Options.Create(new ConnectionManagerOptions
        {
            InstanceId = "test-instance",
            CleanupIntervalSeconds = 60,
            UserTimeoutSeconds = 300
        });

        _clientsMock = new Mock<IHubCallerClients>();
        _callerMock = new Mock<ISingleClientProxy>();
        _allClientsMock = new Mock<IClientProxy>();
        _groupMock = new Mock<IClientProxy>();
        _singleClientMock = new Mock<ISingleClientProxy>();
        _contextMock = new Mock<HubCallerContext>();
        _groupManagerMock = new Mock<IGroupManager>();
        _loggerMock = new Mock<ILogger<ChatHub>>();

        _clientsMock.Setup(c => c.Caller).Returns(_callerMock.Object);
        _clientsMock.Setup(c => c.All).Returns(_allClientsMock.Object);
        _clientsMock.Setup(c => c.Group(It.IsAny<string>())).Returns(_groupMock.Object);
        _clientsMock.Setup(c => c.Client(It.IsAny<string>())).Returns(_singleClientMock.Object);

        _contextMock.Setup(c => c.ConnectionId).Returns(_testConnectionId);

        _hub = new ChatHub(_db, connectionManagerOptions, _loggerMock.Object)
        {
            Clients = _clientsMock.Object,
            Context = _contextMock.Object,
            Groups = _groupManagerMock.Object
        };
    }

    [Fact]
    public async Task Ping_WithNewUser_ShouldCreateUser()
    {
        // Arrange
        var username = "testuser";

        // Act
        await _hub.Ping(username, username);

        // Assert
        var user = await _db.ConnectedUsers
            .FirstOrDefaultAsync(u => u.Username == username && u.ConnectionId == _testConnectionId);

        Assert.NotNull(user);
        Assert.Equal(username, user!.Username);
        Assert.Null(user.Channel);
        Assert.Equal(_testConnectionId, user.ConnectionId);
        Assert.Equal("test-instance", user.ServerInstanceId);
    }

    [Fact]
    public async Task Ping_WithExistingUser_ShouldUpdateLastPing()
    {
        // Arrange
        var username = "testuser";
        var oldPing = DateTime.UtcNow.AddMinutes(-5);

        var user = new ConnectedUser
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid().ToString(),
            Username = username,
            Channel = null,
            ConnectionId = _testConnectionId,
            ConnectedAt = DateTime.UtcNow.AddMinutes(-10),
            LastPing = oldPing,
            ServerInstanceId = "test-instance"
        };

        _db.ConnectedUsers.Add(user);
        await _db.SaveChangesAsync();

        // Act
        await Task.Delay(100);
        await _hub.Ping(username, user.UserId);

        // Assert
        var updatedUser = await _db.ConnectedUsers
            .FirstOrDefaultAsync(u => u.Username == username && u.ConnectionId == _testConnectionId);

        Assert.NotNull(updatedUser);
        Assert.True(updatedUser!.LastPing > oldPing);
    }

    [Fact]
    public async Task JoinChannel_WithNewUser_ShouldNotCreateUser()
    {
        // Arrange
        var username = "testuser";
        var channel = "general";

        var channelEntity = new Channel
        {
            Id = Guid.NewGuid(),
            Name = channel,
            CreatedBy = "admin",
            CreatedAt = DateTime.UtcNow,
            IsMuted = false
        };
        _db.Channels.Add(channelEntity);
        await _db.SaveChangesAsync();

        // Act
        await _hub.JoinChannel(channel);

        // Assert
        var connectedUser = await _db.ConnectedUsers
            .FirstOrDefaultAsync(u => u.Username == username);

        Assert.Null(connectedUser);

        _callerMock.Verify(
            g => g.SendCoreAsync("Error",
                It.Is<object[]>(args => args.Length == 1 && (string)args[0] == "Utilisateur non identifié"), default),
            Times.Once);
    }

    [Fact]
    public async Task JoinChannel_WithNonExistentChannel_ShouldNotifyChannelNotFound()
    {
        // Arrange
        var username = "testuser";
        var channel = "nonexistent";

        var user = new ConnectedUser
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid().ToString(),
            Username = username,
            Channel = null,
            ConnectionId = _testConnectionId,
            ConnectedAt = DateTime.UtcNow.AddMinutes(-10),
            ServerInstanceId = "test-instance"
        };

        _db.ConnectedUsers.Add(user);
        await _db.SaveChangesAsync();

        // Act
        await _hub.JoinChannel(channel);

        // Assert
        _callerMock.Verify(
            c => c.SendCoreAsync(
                "ChannelNotFound",
                It.Is<object[]>(args => args.Length == 1 && (string)args[0] == channel),
                default),
            Times.Once);
    }

    [Fact]
    public async Task JoinChannel_SwitchingChannels_ShouldNotifyBothChannels()
    {
        // Arrange
        var username = "testuser";
        var channel1 = "general";
        var channel2 = "random";

        _db.Channels.AddRange(
            new Channel { Id = Guid.NewGuid(), Name = channel1, CreatedBy = "admin", CreatedAt = DateTime.UtcNow },
            new Channel { Id = Guid.NewGuid(), Name = channel2, CreatedBy = "admin", CreatedAt = DateTime.UtcNow }
        );

        var user = new ConnectedUser
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid().ToString(),
            Username = username,
            Channel = channel1,
            ConnectionId = _testConnectionId,
            ConnectedAt = DateTime.UtcNow,
            LastPing = DateTime.UtcNow,
            ServerInstanceId = "test-instance"
        };

        _db.ConnectedUsers.Add(user);
        await _db.SaveChangesAsync();

        // Act
        await _hub.JoinChannel(channel2);

        // Assert
        var updatedUser = await _db.ConnectedUsers
            .FirstOrDefaultAsync(u => u.Username == username);

        Assert.NotNull(updatedUser);
        Assert.Equal(channel2, updatedUser!.Channel);

        _groupManagerMock.Verify(
            g => g.RemoveFromGroupAsync(_testConnectionId, channel1, default),
            Times.Once);

        _groupMock.Verify(
            g => g.SendCoreAsync("UserLeft", It.Is<object[]>(args => (string)args[2] == channel1), default),
            Times.Once);

        _groupMock.Verify(
            g => g.SendCoreAsync("UserJoined", It.Is<object[]>(args => (string)args[2] == channel2), default),
            Times.Once);
    }

    [Fact]
    public async Task JoinChannel_SameChannel_ShouldNotNotifyLeave()
    {
        // Arrange
        var username = "testuser";
        var channel = "general";

        _db.Channels.Add(new Channel
        {
            Id = Guid.NewGuid(),
            Name = channel,
            CreatedBy = "admin",
            CreatedAt = DateTime.UtcNow
        });

        var user = new ConnectedUser
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid().ToString(),
            Username = username,
            Channel = channel,
            ConnectionId = _testConnectionId,
            ConnectedAt = DateTime.UtcNow,
            LastPing = DateTime.UtcNow,
            ServerInstanceId = "test-instance"
        };

        _db.ConnectedUsers.Add(user);
        await _db.SaveChangesAsync();

        // Act
        await _hub.JoinChannel(channel);

        // Assert
        _groupManagerMock.Verify(
            g => g.RemoveFromGroupAsync(It.IsAny<string>(), It.IsAny<string>(), default),
            Times.Never);

        _groupMock.Verify(
            g => g.SendCoreAsync("UserLeft", It.IsAny<object[]>(), default),
            Times.Never);
    }

    [Fact]
    public async Task LeaveChannel_ShouldSetChannelToNull()
    {
        // Arrange
        var username = "testuser";
        var channel = "general";

        var user = new ConnectedUser
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid().ToString(),
            Username = username,
            Channel = channel,
            ConnectionId = _testConnectionId,
            ConnectedAt = DateTime.UtcNow,
            LastPing = DateTime.UtcNow,
            ServerInstanceId = "test-instance"
        };

        _db.ConnectedUsers.Add(user);
        await _db.SaveChangesAsync();

        // Act
        await _hub.LeaveChannel(channel);

        // Assert
        var updatedUser = await _db.ConnectedUsers
            .FirstOrDefaultAsync(u => u.Username == username && u.ConnectionId == _testConnectionId);

        Assert.NotNull(updatedUser);
        Assert.Null(updatedUser!.Channel);

        _groupManagerMock.Verify(
            g => g.RemoveFromGroupAsync(_testConnectionId, channel, default),
            Times.Once);

        _groupMock.Verify(
            g => g.SendCoreAsync("UserLeft", It.Is<object[]>(args => args.Length == 3), default),
            Times.Once);
    }

    [Fact]
    public async Task LeaveChannel_WithWrongChannel_ShouldNotModify()
    {
        // Arrange
        var username = "testuser";
        var actualChannel = "general";
        var wrongChannel = "random";

        var user = new ConnectedUser
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid().ToString(),
            Username = username,
            Channel = actualChannel,
            ConnectionId = _testConnectionId,
            ConnectedAt = DateTime.UtcNow,
            LastPing = DateTime.UtcNow,
            ServerInstanceId = "test-instance"
        };

        _db.ConnectedUsers.Add(user);
        await _db.SaveChangesAsync();

        // Act
        await _hub.LeaveChannel(wrongChannel);

        // Assert
        var updatedUser = await _db.ConnectedUsers
            .FirstOrDefaultAsync(u => u.Username == username);

        Assert.NotNull(updatedUser);
        Assert.Equal(actualChannel, updatedUser!.Channel);

        _groupManagerMock.Verify(
            g => g.RemoveFromGroupAsync(It.IsAny<string>(), It.IsAny<string>(), default),
            Times.Never);
    }


    [Fact]
    public async Task SendMessage_ShouldSaveAndBroadcastMessage()
    {
        // Arrange
        var channel = "general";

        var channelEntity = new Channel
        {
            Id = Guid.NewGuid(),
            Name = channel,
            CreatedBy = "admin",
            CreatedAt = DateTime.UtcNow,
            IsMuted = false
        };
        _db.Channels.Add(channelEntity);
        var connectorUser = new ConnectedUser
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid().ToString(),
            Username = "sender",
            Channel = channel,
            ConnectionId = _testConnectionId,
            ConnectedAt = DateTime.UtcNow,
            LastPing = DateTime.UtcNow,
            ServerInstanceId = "test-instance"
        };
        _db.ConnectedUsers.Add(connectorUser);
        await _db.SaveChangesAsync();

        var messageRequest = new SendMessageRequest
        {
            Content = "Hello, World!",
            Channel = channel
        };

        // Act
        await _hub.SendMessage(messageRequest);

        // Assert
        var message = await _db.Messages
            .FirstOrDefaultAsync(m => m.UserId == connectorUser.UserId && m.Content == messageRequest.Content);

        Assert.NotNull(message);
        Assert.Equal(messageRequest.Channel, message!.Channel);
        Assert.False(message.IsDeleted);

        _groupMock.Verify(
            g => g.SendCoreAsync("ReceiveMessage", It.Is<object[]>(args => args.Length == 1), default),
            Times.Once);
    }

    [Fact]
    public async Task SendMessage_WithConnectedUser_ShouldUpdateLastActivity()
    {
        // Arrange
        var channel = "general";
        var username = "sender";

        _db.Channels.Add(new Channel
        {
            Id = Guid.NewGuid(),
            Name = channel,
            CreatedBy = "admin",
            CreatedAt = DateTime.UtcNow,
            IsMuted = false
        });

        var user = new ConnectedUser
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid().ToString(),
            Username = username,
            Channel = channel,
            ConnectionId = _testConnectionId,
            ConnectedAt = DateTime.UtcNow.AddMinutes(-10),
            LastPing = DateTime.UtcNow.AddMinutes(-5),
            LastActivity = DateTime.UtcNow.AddMinutes(-10),
            ServerInstanceId = "test-instance"
        };

        _db.ConnectedUsers.Add(user);
        await _db.SaveChangesAsync();

        var oldActivity = user.LastActivity;

        var messageRequest = new SendMessageRequest
        {
            Content = "Test message",
            Channel = channel
        };

        // Act
        await Task.Delay(100);
        await _hub.SendMessage(messageRequest);

        // Assert
        var updatedUser = await _db.ConnectedUsers
            .FirstOrDefaultAsync(u => u.UserId == user.UserId);

        Assert.NotNull(updatedUser);
        Assert.True(updatedUser!.LastActivity > oldActivity);
    }

    [Fact]
    public async Task SendMessage_ToMutedChannel_ShouldBlockNonCreatorNonAdmin()
    {
        // Arrange
        var channel = "muted-channel";

        var mutedChannel = new Shared.Models.Channel
        {
            Id = Guid.NewGuid(),
            Name = channel,
            CreatedBy = "admin",
            CreatedAt = DateTime.UtcNow,
            IsMuted = true
        };

        var user = new ConnectedUser
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid().ToString(),
            Username = "user-123",
            Channel = channel,
            ConnectionId = _testConnectionId,
            ConnectedAt = DateTime.UtcNow.AddMinutes(-10),
            LastPing = DateTime.UtcNow.AddMinutes(-5),
            LastActivity = DateTime.UtcNow.AddMinutes(-10),
            ServerInstanceId = "test-instance"
        };

        _db.ConnectedUsers.Add(user);

        _db.Channels.Add(mutedChannel);
        await _db.SaveChangesAsync();

        var messageRequest = new SendMessageRequest
        {
            Content = "This should not be sent",
            Channel = channel
        };

        // Act
        await _hub.SendMessage(messageRequest);

        // Assert
        var message = await _db.Messages
            .FirstOrDefaultAsync(m => m.UserId == user.UserId && m.Content == messageRequest.Content);

        Assert.Null(message);

        _callerMock.Verify(
            c => c.SendCoreAsync("MessageBlocked", It.Is<object[]>(args => args.Length == 1), default),
            Times.Once);
    }

    [Fact]
    public async Task SendMessage_ToMutedChannel_CreatorCanSend()
    {
        // Arrange
        var channel = "muted-channel";
        var creatorUsername = "creator";

        var mutedChannel = new Channel
        {
            Id = Guid.NewGuid(),
            Name = channel,
            CreatedBy = creatorUsername,
            CreatedAt = DateTime.UtcNow,
            IsMuted = true
        };

        var user = new ConnectedUser
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid().ToString(),
            Username = creatorUsername,
            Channel = channel,
            ConnectionId = _testConnectionId,
            ConnectedAt = DateTime.UtcNow.AddMinutes(-10),
            LastPing = DateTime.UtcNow.AddMinutes(-5),
            LastActivity = DateTime.UtcNow.AddMinutes(-10),
            ServerInstanceId = "test-instance"
        };

        _db.ConnectedUsers.Add(user);

        _db.Channels.Add(mutedChannel);
        await _db.SaveChangesAsync();

        var messageRequest = new SendMessageRequest
        {
            Content = "Creator can send",
            Channel = channel
        };

        // Act
        await _hub.SendMessage(messageRequest);

        // Assert
        var message = await _db.Messages
            .FirstOrDefaultAsync(m => m.UserId == user.UserId && m.Content == messageRequest.Content);

        Assert.NotNull(message);
        Assert.Equal("Creator can send", message!.Content);

        _groupMock.Verify(
            g => g.SendCoreAsync("ReceiveMessage", It.Is<object[]>(args => args.Length == 1), default),
            Times.Once);
    }

    [Fact]
    public async Task SendPrivateMessage_ShouldSaveAndSendToRecipient()
    {
        // Arrange
        var recipientUser = new ConnectedUser
        {
            Id = Guid.NewGuid(),
            Username = "recipient",
            UserId = Guid.NewGuid().ToString(),
            Channel = "general",
            ConnectionId = "recipient-connection-id",
            ConnectedAt = DateTime.UtcNow,
            LastPing = DateTime.UtcNow,
            ServerInstanceId = "test-instance"
        };

        _db.ConnectedUsers.Add(recipientUser);

        var user = new ConnectedUser
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid().ToString(),
            Username = "sender",
            Channel = "general",
            ConnectionId = _testConnectionId,
            ConnectedAt = DateTime.UtcNow.AddMinutes(-10),
            LastPing = DateTime.UtcNow.AddMinutes(-5),
            LastActivity = DateTime.UtcNow.AddMinutes(-10),
            ServerInstanceId = "test-instance"
        };

        _db.ConnectedUsers.Add(user);

        await _db.SaveChangesAsync();

        var messageRequest = new SendPrivateMessageRequest
        {
            RecipientUserId = recipientUser.UserId,
            RecipientUsername = "recipient",
            Content = "Private message"
        };

        // Act
        await _hub.SendPrivateMessage(messageRequest);

        // Assert
        var message = await _db.PrivateMessages
            .FirstOrDefaultAsync(m =>
                m.SenderUserId == user.UserId &&
                m.RecipientUserId == messageRequest.RecipientUserId);

        Assert.NotNull(message);
        Assert.Equal(messageRequest.Content, message!.Content);
        Assert.False(message.IsRead);

        _singleClientMock.Verify(
            c => c.SendCoreAsync("ReceivePrivateMessage", It.Is<object[]>(args => args.Length == 1), default),
            Times.Once);

        _callerMock.Verify(
            c => c.SendCoreAsync("PrivateMessageSent", It.Is<object[]>(args => args.Length == 1), default),
            Times.Once);
    }

    [Fact]
    public async Task SendPrivateMessage_WithOfflineRecipient_ShouldOnlyNotifySender()
    {
        // Arrange
        var user = new ConnectedUser
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid().ToString(),
            Username = "sender",
            Channel = "general",
            ConnectionId = _testConnectionId,
            ConnectedAt = DateTime.UtcNow.AddMinutes(-10),
            LastPing = DateTime.UtcNow.AddMinutes(-5),
            LastActivity = DateTime.UtcNow.AddMinutes(-10),
            ServerInstanceId = "test-instance"
        };

        _db.ConnectedUsers.Add(user);
        await _db.SaveChangesAsync();

        var messageRequest = new SendPrivateMessageRequest
        {
            RecipientUserId = "offline_recipient",
            RecipientUsername = "offline_recipient",
            Content = "Message to offline user"
        };

        // Act
        await _hub.SendPrivateMessage(messageRequest);

        // Assert
        var message = await _db.PrivateMessages
            .FirstOrDefaultAsync(m => m.SenderUserId == user.UserId && m.RecipientUserId == "offline_recipient");

        Assert.NotNull(message);

        _singleClientMock.Verify(
            c => c.SendCoreAsync("ReceivePrivateMessage", It.IsAny<object[]>(), default),
            Times.Never);

        _callerMock.Verify(
            c => c.SendCoreAsync("PrivateMessageSent", It.IsAny<object[]>(), default),
            Times.Once);
    }

    [Fact]
    public async Task MarkPrivateMessagesAsRead_ShouldMarkMessagesAndNotifySender()
    {
        // Arrange
        var senderUsername = "sender";
        var recipientUsername = "recipient";

        var recipientUser = new ConnectedUser
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid().ToString(),
            Username = recipientUsername,
            Channel = "general",
            ConnectionId = _testConnectionId,
            ConnectedAt = DateTime.UtcNow,
            LastPing = DateTime.UtcNow,
            ServerInstanceId = "test-instance"
        };

        var senderUser = new ConnectedUser
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid().ToString(),
            Username = senderUsername,
            Channel = "general",
            ConnectionId = "sender-connection-id",
            ConnectedAt = DateTime.UtcNow,
            LastPing = DateTime.UtcNow,
            ServerInstanceId = "test-instance"
        };

        _db.ConnectedUsers.AddRange(recipientUser, senderUser);

        var unreadMessage = new PrivateMessage
        {
            Id = Guid.NewGuid(),
            SenderUserId = senderUser.UserId,
            SenderUsername = senderUsername,
            RecipientUserId = recipientUser.UserId,
            RecipientUsername = recipientUsername,
            Content = "Unread message",
            Timestamp = DateTime.UtcNow,
            IsRead = false,
        };

        _db.PrivateMessages.Add(unreadMessage);
        await _db.SaveChangesAsync();

        // Act
        await _hub.MarkPrivateMessagesAsRead(senderUser.UserId);

        // Assert
        var message = await _db.PrivateMessages.FindAsync(unreadMessage.Id);
        Assert.NotNull(message);
        Assert.True(message!.IsRead);

        _singleClientMock.Verify(
            c => c.SendCoreAsync(
                "PrivateMessagesRead",
                It.Is<object[]>(args => args.Length == 2 && (string)args[0] == recipientUsername),
                default),
            Times.Once);
    }

    [Fact]
    public async Task OnDisconnectedAsync_ShouldRemoveUser()
    {
        // Arrange
        var username = "disconnectuser";
        var channel = "general";

        var user = new ConnectedUser
        {
            Id = Guid.NewGuid(),
            Username = username,
            Channel = channel,
            ConnectionId = _testConnectionId,
            ConnectedAt = DateTime.UtcNow,
            LastPing = DateTime.UtcNow,
            ServerInstanceId = "test-instance"
        };

        _db.ConnectedUsers.Add(user);
        await _db.SaveChangesAsync();

        // Act
        await _hub.OnDisconnectedAsync(null);

        // Assert
        var removedUser = await _db.ConnectedUsers
            .FirstOrDefaultAsync(u => u.ConnectionId == _testConnectionId);

        Assert.Null(removedUser);
    }

    [Fact]
    public async Task OnDisconnectedAsync_WithChannel_ShouldNotifyChannel()
    {
        // Arrange
        var username = "disconnectuser";
        var channel = "general";

        var user = new ConnectedUser
        {
            Id = Guid.NewGuid(),
            Username = username,
            Channel = channel,
            ConnectionId = _testConnectionId,
            ConnectedAt = DateTime.UtcNow,
            LastPing = DateTime.UtcNow,
            ServerInstanceId = "test-instance"
        };

        _db.ConnectedUsers.Add(user);
        await _db.SaveChangesAsync();

        // Act
        await _hub.OnDisconnectedAsync(null);

        // Assert
        _groupMock.Verify(
            g => g.SendCoreAsync("UserLeft", It.IsAny<object[]>(), default),
            Times.Once);
    }

    [Fact]
    public async Task OnDisconnectedAsync_WithoutChannel_ShouldNotNotify()
    {
        // Arrange
        var username = "disconnectuser";

        var user = new ConnectedUser
        {
            Id = Guid.NewGuid(),
            Username = username,
            Channel = null,
            ConnectionId = _testConnectionId,
            ConnectedAt = DateTime.UtcNow,
            LastPing = DateTime.UtcNow,
            ServerInstanceId = "test-instance"
        };

        _db.ConnectedUsers.Add(user);
        await _db.SaveChangesAsync();

        // Act
        await _hub.OnDisconnectedAsync(null);

        // Assert
        _groupMock.Verify(
            g => g.SendCoreAsync("UserLeft", It.IsAny<object[]>(), default),
            Times.Never);
    }

    [Fact]
    public async Task ConnectedUser_WithoutChannel_ShouldStillReserveUsername()
    {
        // Arrange
        var username = "nomad";

        var user = new ConnectedUser
        {
            Id = Guid.NewGuid(),
            Username = username,
            Channel = null,
            ConnectionId = _testConnectionId,
            ConnectedAt = DateTime.UtcNow,
            LastPing = DateTime.UtcNow,
            ServerInstanceId = "test-instance"
        };

        _db.ConnectedUsers.Add(user);
        await _db.SaveChangesAsync();

        // Act
        var exists = await _db.ConnectedUsers
            .AnyAsync(u => u.Username == username);

        // Assert
        Assert.True(exists);
    }

    [Fact]
    public async Task OnDisconnectedAsync_WithLastConnection_ShouldNotifyUserIsOffline()
    {
        // Arrange
        var username = "testuser";
        var connectionId = "conn-123";
        var channel = "general";

        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, username)
        };

        var claimsPrincipal = new ClaimsPrincipal(new ClaimsIdentity(claims));

        _contextMock.Setup(c => c.ConnectionId).Returns(connectionId);
        _contextMock.Setup(c => c.User).Returns(claimsPrincipal);

        // Ajouter l'utilisateur connecté
        var connectedUser = new ConnectedUser
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid().ToString(),
            Username = username,
            ConnectionId = connectionId,
            Channel = channel,
            ConnectedAt = DateTime.UtcNow,
            LastActivity = DateTime.UtcNow,
            LastPing = DateTime.UtcNow,
            ServerInstanceId = "server-1"
        };

        _db.ConnectedUsers.Add(connectedUser);
        await _db.SaveChangesAsync();

        // Act
        await _hub.OnDisconnectedAsync(null);

        // Assert
        // Vérifier que l'utilisateur a été supprimé de ConnectedUsers
        var userExists = await _db.ConnectedUsers
            .AnyAsync(u => u.ConnectionId == connectionId);
        Assert.False(userExists);

        // Vérifier que la notification offline a été envoyée
        _allClientsMock.Verify(
            c => c.SendCoreAsync(
                "UserStatusChanged",
                It.Is<object[]>(o => o.Length == 3 && (string)o[0] == username && !(bool)o[2]),
                default),
            Times.Once);

        // Vérifier que UserLeft a été appelé pour le canal
        _groupMock.Verify(
            c => c.SendCoreAsync(
                "UserLeft",
                It.Is<object[]>(o => o.Length == 3 && (string)o[0] == username && (string)o[2] == channel),
                default),
            Times.Once);
    }

    [Fact]
    public async Task OnDisconnectedAsync_WithMultipleConnections_ShouldNotNotifyOffline()
    {
        // Arrange
        var username = "testuser";
        var connectionId1 = "conn-123";
        var connectionId2 = "conn-456";

        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, username)
        };

        var claimsPrincipal = new ClaimsPrincipal(new ClaimsIdentity(claims));

        _contextMock.Setup(c => c.ConnectionId).Returns(connectionId1);
        _contextMock.Setup(c => c.User).Returns(claimsPrincipal);

        // Ajouter deux connexions pour le même utilisateur
        _db.ConnectedUsers.AddRange(
            new ConnectedUser
            {
                Id = Guid.NewGuid(),
                Username = username,
                ConnectionId = connectionId1,
                Channel = "general",
                ConnectedAt = DateTime.UtcNow,
                LastActivity = DateTime.UtcNow,
                LastPing = DateTime.UtcNow,
                ServerInstanceId = "server-1"
            },
            new ConnectedUser
            {
                Id = Guid.NewGuid(),
                Username = username,
                ConnectionId = connectionId2,
                Channel = "random",
                ConnectedAt = DateTime.UtcNow,
                LastActivity = DateTime.UtcNow,
                LastPing = DateTime.UtcNow,
                ServerInstanceId = "server-1"
            });

        await _db.SaveChangesAsync();

        // Act
        await _hub.OnDisconnectedAsync(null);

        // Assert
        // La notification offline ne doit PAS être envoyée car il reste une autre connexion
        _allClientsMock.Verify(
            c => c.SendCoreAsync(
                "UserStatusChanged",
                It.Is<object[]>(o => o.Length == 2 && !(bool)o[1]),
                default),
            Times.Never);

        // Vérifier qu'il reste une connexion
        var remainingConnections = await _db.ConnectedUsers
            .CountAsync(u => u.Username == username);
        Assert.Equal(1, remainingConnections);
    }

    [Fact]
    public async Task OnDisconnectedAsync_WithoutChannel_ShouldStillNotifyOffline()
    {
        // Arrange
        var username = "testuser";
        var connectionId = "conn-123";

        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, username)
        };

        var claimsPrincipal = new ClaimsPrincipal(new ClaimsIdentity(claims));

        _contextMock.Setup(c => c.ConnectionId).Returns(connectionId);
        _contextMock.Setup(c => c.User).Returns(claimsPrincipal);

        // Ajouter l'utilisateur sans canal (pas encore rejoint de canal)
        var connectedUser = new ConnectedUser
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid().ToString(),
            Username = username,
            ConnectionId = connectionId,
            Channel = null, // Pas de canal
            ConnectedAt = DateTime.UtcNow,
            LastActivity = DateTime.UtcNow,
            LastPing = DateTime.UtcNow,
            ServerInstanceId = "server-1"
        };

        _db.ConnectedUsers.Add(connectedUser);
        await _db.SaveChangesAsync();

        // Act
        await _hub.OnDisconnectedAsync(null);

        // Assert
        _allClientsMock.Verify(
            c => c.SendCoreAsync(
                "UserStatusChanged",
                It.Is<object[]>(o => o.Length == 3 && (string)o[0] == username && (string)o[1] == connectedUser.UserId && !(bool)o[2]),
                default),
            Times.Once);

        // UserLeft ne doit PAS être appelé car il n'y avait pas de canal
        _groupMock.Verify(
            c => c.SendCoreAsync(
                "UserLeft",
                It.IsAny<object[]>(),
                default),
            Times.Never);
    }

    [Fact]
    public async Task OnConnectedAsync_WithoutUsername_ShouldNotNotifyUserStatus()
    {
        // Arrange
        var connectionId = "conn-123";
        var claimsPrincipal = new ClaimsPrincipal(new ClaimsIdentity()); // Pas de claims

        _contextMock.Setup(c => c.ConnectionId).Returns(connectionId);
        _contextMock.Setup(c => c.User).Returns(claimsPrincipal);

        // Act
        await _hub.OnConnectedAsync();

        // Assert
        _allClientsMock.Verify(
            c => c.SendCoreAsync(
                "UserStatusChanged",
                It.IsAny<object[]>(),
                default),
            Times.Never);
    }

    [Fact]
    public async Task SendMessage_WithMutedUser_ShouldSaveButNotBroadcast()
    {
        // Arrange
        var channel = "general";

        var channelEntity = new Channel
        {
            Id = Guid.NewGuid(),
            Name = channel,
            CreatedBy = "admin",
            CreatedAt = DateTime.UtcNow,
            IsMuted = false
        };
        _db.Channels.Add(channelEntity);

        var connectedUser = new ConnectedUser
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid().ToString(),
            Username = "muteduser",
            Channel = channel,
            ConnectionId = _testConnectionId,
            ConnectedAt = DateTime.UtcNow,
            LastPing = DateTime.UtcNow,
            ServerInstanceId = "test-instance"
        };
        _db.ConnectedUsers.Add(connectedUser);

        // Muter l'utilisateur
        var mute = new MutedUser
        {
            Id = Guid.NewGuid(),
            ChannelName = channel,
            UserId = connectedUser.UserId,
            MutedByUserId = "admin-id",
            MutedAt = DateTime.UtcNow,
            Reason = "Spam"
        };
        _db.MutedUsers.Add(mute);
        await _db.SaveChangesAsync();

        var messageRequest = new SendMessageRequest
        {
            Content = "This message should be saved but not broadcast",
            Channel = channel
        };

        // Act
        await _hub.SendMessage(messageRequest);

        // Assert
        // Vérifier que le message est bien sauvegardé en BDD
        var message = await _db.Messages
            .FirstOrDefaultAsync(m => m.UserId == connectedUser.UserId && m.Content == messageRequest.Content);

        Assert.NotNull(message);
        Assert.Equal(messageRequest.Channel, message!.Channel);
        Assert.False(message.IsDeleted);

        // Vérifier qu'AUCUN message n'a été envoyé au groupe
        _groupMock.Verify(
            g => g.SendCoreAsync("ReceiveMessage", It.IsAny<object[]>(), default),
            Times.Never);

        // Vérifier qu'AUCUN message n'a été envoyé à l'appelant
        _callerMock.Verify(
            c => c.SendCoreAsync("ReceiveMessage", It.IsAny<object[]>(), default),
            Times.Never);
    }

    [Fact]
    public async Task SendMessage_WithMutedUserInMutedChannel_ShouldNotBroadcast()
    {
        // Arrange
        var channel = "muted-channel";

        var mutedChannel = new Channel
        {
            Id = Guid.NewGuid(),
            Name = channel,
            CreatedBy = "admin",
            CreatedAt = DateTime.UtcNow,
            IsMuted = true // Canal mute
        };
        _db.Channels.Add(mutedChannel);

        var connectedUser = new ConnectedUser
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid().ToString(),
            Username = "muteduser",
            Channel = channel,
            ConnectionId = _testConnectionId,
            ConnectedAt = DateTime.UtcNow,
            LastPing = DateTime.UtcNow,
            ServerInstanceId = "test-instance"
        };
        _db.ConnectedUsers.Add(connectedUser);

        // Utilisateur mute individuellement
        var mute = new MutedUser
        {
            Id = Guid.NewGuid(),
            ChannelName = channel,
            UserId = connectedUser.UserId,
            MutedByUserId = "admin-id",
            MutedAt = DateTime.UtcNow
        };
        _db.MutedUsers.Add(mute);
        await _db.SaveChangesAsync();

        var messageRequest = new SendMessageRequest
        {
            Content = "Double mute test",
            Channel = channel
        };

        // Act
        await _hub.SendMessage(messageRequest);

        // Assert
        // Message sauvegardé
        var message = await _db.Messages
            .FirstOrDefaultAsync(m => m.UserId == connectedUser.UserId);
        Assert.NotNull(message);

        // Pas de broadcast
        _groupMock.Verify(
            g => g.SendCoreAsync("ReceiveMessage", It.IsAny<object[]>(), default),
            Times.Never);

        // Pas de notification MessageBlocked car l'utilisateur est mute individuellement
        _callerMock.Verify(
            c => c.SendCoreAsync("MessageBlocked", It.IsAny<object[]>(), default),
            Times.Never);
    }

    [Fact]
    public async Task SendMessage_WithNonMutedUserInNormalChannel_ShouldBroadcast()
    {
        // Arrange
        var channel = "general";

        var channelEntity = new Channel
        {
            Id = Guid.NewGuid(),
            Name = channel,
            CreatedBy = "admin",
            CreatedAt = DateTime.UtcNow,
            IsMuted = false
        };
        _db.Channels.Add(channelEntity);

        var connectedUser = new ConnectedUser
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid().ToString(),
            Username = "normaluser",
            Channel = channel,
            ConnectionId = _testConnectionId,
            ConnectedAt = DateTime.UtcNow,
            LastPing = DateTime.UtcNow,
            ServerInstanceId = "test-instance"
        };
        _db.ConnectedUsers.Add(connectedUser);
        await _db.SaveChangesAsync();

        var messageRequest = new SendMessageRequest
        {
            Content = "Normal message",
            Channel = channel
        };

        // Act
        await _hub.SendMessage(messageRequest);

        // Assert
        // Message sauvegardé
        var message = await _db.Messages
            .FirstOrDefaultAsync(m => m.UserId == connectedUser.UserId);
        Assert.NotNull(message);

        // Message diffusé au groupe
        _groupMock.Verify(
            g => g.SendCoreAsync("ReceiveMessage", It.Is<object[]>(args => args.Length == 1), default),
            Times.Once);
    }

    [Fact]
    public async Task SendMessage_MutedUserThenUnmuted_ShouldAllowBroadcast()
    {
        // Arrange
        var channel = "general";

        var channelEntity = new Channel
        {
            Id = Guid.NewGuid(),
            Name = channel,
            CreatedBy = "admin",
            CreatedAt = DateTime.UtcNow,
            IsMuted = false
        };
        _db.Channels.Add(channelEntity);

        var connectedUser = new ConnectedUser
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid().ToString(),
            Username = "testuser",
            Channel = channel,
            ConnectionId = _testConnectionId,
            ConnectedAt = DateTime.UtcNow,
            LastPing = DateTime.UtcNow,
            ServerInstanceId = "test-instance"
        };
        _db.ConnectedUsers.Add(connectedUser);

        // Muter puis démuter
        var mute = new MutedUser
        {
            Id = Guid.NewGuid(),
            ChannelName = channel,
            UserId = connectedUser.UserId,
            MutedByUserId = "admin-id",
            MutedAt = DateTime.UtcNow
        };
        _db.MutedUsers.Add(mute);
        await _db.SaveChangesAsync();

        // Premier message (mute)
        var firstMessage = new SendMessageRequest
        {
            Content = "First message while muted",
            Channel = channel
        };
        await _hub.SendMessage(firstMessage);

        // Vérifier pas de broadcast
        _groupMock.Verify(
            g => g.SendCoreAsync("ReceiveMessage", It.IsAny<object[]>(), default),
            Times.Never);

        // Démuter
        _db.MutedUsers.Remove(mute);
        await _db.SaveChangesAsync();

        // Deuxième message (non mute)
        var secondMessage = new SendMessageRequest
        {
            Content = "Second message after unmute",
            Channel = channel
        };
        await _hub.SendMessage(secondMessage);

        // Assert
        // Les deux messages sont en BDD
        var messages = await _db.Messages
            .Where(m => m.UserId == connectedUser.UserId)
            .ToListAsync();
        Assert.Equal(2, messages.Count);

        // Le deuxième message a été diffusé
        _groupMock.Verify(
            g => g.SendCoreAsync("ReceiveMessage", It.Is<object[]>(args => args.Length == 1), default),
            Times.Once);
    }

    [Fact]
    public async Task SendMessage_AdminInMutedChannel_CanStillSendEvenIfMuted()
    {
        // Arrange
        var channel = "muted-channel";

        var mutedChannel = new Channel
        {
            Id = Guid.NewGuid(),
            Name = channel,
            CreatedBy = "creator",
            CreatedAt = DateTime.UtcNow,
            IsMuted = true
        };
        _db.Channels.Add(mutedChannel);

        var adminUser = new ReservedUsername
        {
            Id = Guid.NewGuid(),
            Username = "admin",
            Email = "admin@test.com",
            Provider = ExternalAuthProvider.Google,
            ExternalUserId = "ext-admin",
            CreatedAt = DateTime.UtcNow,
            LastLoginAt = DateTime.UtcNow,
            IsAdmin = true
        };
        _db.ReservedUsernames.Add(adminUser);

        var connectedUser = new ConnectedUser
        {
            Id = Guid.NewGuid(),
            UserId = adminUser.Id.ToString(),
            Username = adminUser.Username,
            Channel = channel,
            ConnectionId = _testConnectionId,
            ConnectedAt = DateTime.UtcNow,
            LastPing = DateTime.UtcNow,
            ServerInstanceId = "test-instance"
        };
        _db.ConnectedUsers.Add(connectedUser);

        // Admin mute individuellement (rare mais possible)
        var mute = new MutedUser
        {
            Id = Guid.NewGuid(),
            ChannelName = channel,
            UserId = connectedUser.UserId,
            MutedByUserId = "creator-id",
            MutedAt = DateTime.UtcNow
        };
        _db.MutedUsers.Add(mute);
        await _db.SaveChangesAsync();

        var messageRequest = new SendMessageRequest
        {
            Content = "Admin message in muted channel",
            Channel = channel
        };

        // Act
        await _hub.SendMessage(messageRequest);

        // Assert
        // Message sauvegardé
        var message = await _db.Messages
            .FirstOrDefaultAsync(m => m.UserId == connectedUser.UserId);
        Assert.NotNull(message);

        // Pas de broadcast car l'utilisateur est mute individuellement
        // (le mute individuel prend le dessus sur le statut admin)
        _groupMock.Verify(
            g => g.SendCoreAsync("ReceiveMessage", It.IsAny<object[]>(), default),
            Times.Never);
    }

    public async ValueTask DisposeAsync()
    {
        await _db.DisposeAsync();
        GC.SuppressFinalize(this);
    }
}