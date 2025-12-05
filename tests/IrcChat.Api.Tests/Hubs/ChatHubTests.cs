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
    private readonly ChatDbContext db;
    private readonly Mock<IHubCallerClients> clientsMock;
    private readonly Mock<ISingleClientProxy> callerMock;
    private readonly Mock<IClientProxy> allClientsMock;
    private readonly Mock<IClientProxy> groupMock;
    private readonly Mock<ISingleClientProxy> singleClientMock;
    private readonly Mock<HubCallerContext> contextMock;
    private readonly Mock<IGroupManager> groupManagerMock;
    private readonly Mock<ILogger<ChatHub>> loggerMock;
    private readonly ChatHub hub;
    private readonly string testConnectionId = "test-connection-id";

    public ChatHubTests()
    {
        var options = new DbContextOptionsBuilder<ChatDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        db = new ChatDbContext(options);

        var connectionManagerOptions = Options.Create(new ConnectionManagerOptions
        {
            InstanceId = "test-instance",
            CleanupIntervalSeconds = 60,
            UserTimeoutSeconds = 300,
        });

        clientsMock = new Mock<IHubCallerClients>();
        callerMock = new Mock<ISingleClientProxy>();
        allClientsMock = new Mock<IClientProxy>();
        groupMock = new Mock<IClientProxy>();
        singleClientMock = new Mock<ISingleClientProxy>();
        contextMock = new Mock<HubCallerContext>();
        groupManagerMock = new Mock<IGroupManager>();
        loggerMock = new Mock<ILogger<ChatHub>>();

        clientsMock.Setup(c => c.Caller).Returns(callerMock.Object);
        clientsMock.Setup(c => c.All).Returns(allClientsMock.Object);
        clientsMock.Setup(c => c.Group(It.IsAny<string>())).Returns(groupMock.Object);
        clientsMock.Setup(c => c.Client(It.IsAny<string>())).Returns(singleClientMock.Object);

        contextMock.Setup(c => c.ConnectionId).Returns(testConnectionId);

        hub = new ChatHub(db, connectionManagerOptions, loggerMock.Object)
        {
            Clients = clientsMock.Object,
            Context = contextMock.Object,
            Groups = groupManagerMock.Object,
        };
    }

    [Fact]
    public async Task Ping_WithNewUser_ShouldCreateUser()
    {
        // Arrange
        var username = "testuser";

        // Act
        await hub.Ping(username, username);

        // Assert
        var user = await db.ConnectedUsers
            .FirstOrDefaultAsync(u => u.Username == username && u.ConnectionId == testConnectionId);

        Assert.NotNull(user);
        Assert.Equal(username, user!.Username);
        Assert.Null(user.Channel);
        Assert.Equal(testConnectionId, user.ConnectionId);
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
            ConnectionId = testConnectionId,
            ConnectedAt = DateTime.UtcNow.AddMinutes(-10),
            LastActivity = oldPing,
            ServerInstanceId = "test-instance",
        };

        db.ConnectedUsers.Add(user);
        await db.SaveChangesAsync();

        // Act
        await Task.Delay(100);
        await hub.Ping(username, user.UserId);

        // Assert
        var updatedUser = await db.ConnectedUsers
            .FirstOrDefaultAsync(u => u.Username == username && u.ConnectionId == testConnectionId);

        Assert.NotNull(updatedUser);
        Assert.True(updatedUser!.LastActivity > oldPing);
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
            IsMuted = false,
        };
        db.Channels.Add(channelEntity);
        await db.SaveChangesAsync();

        // Act
        await hub.JoinChannel(channel);

        // Assert
        var connectedUser = await db.ConnectedUsers
            .FirstOrDefaultAsync(u => u.Username == username);

        Assert.Null(connectedUser);

        callerMock.Verify(
            g => g.SendCoreAsync(
                    "Error",
                    It.Is<object[]>(args => args.Length == 1 && (string)args[0] == "Utilisateur non identifié"),
                    default),
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
            ConnectionId = testConnectionId,
            ConnectedAt = DateTime.UtcNow.AddMinutes(-10),
            ServerInstanceId = "test-instance",
        };

        db.ConnectedUsers.Add(user);
        await db.SaveChangesAsync();

        // Act
        await hub.JoinChannel(channel);

        // Assert
        callerMock.Verify(
            c => c.SendCoreAsync(
                "ChannelNotFound",
                It.Is<object[]>(args => args.Length == 1 && (string)args[0] == channel),
                default),
            Times.Once);
    }

    [Fact]
    public async Task JoinChannel_SameChannel_ShouldNotNotifyLeave()
    {
        // Arrange
        var username = "testuser";
        var channel = "general";

        db.Channels.Add(new Channel
        {
            Id = Guid.NewGuid(),
            Name = channel,
            CreatedBy = "admin",
            CreatedAt = DateTime.UtcNow,
        });

        var user = new ConnectedUser
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid().ToString(),
            Username = username,
            Channel = channel,
            ConnectionId = testConnectionId,
            ConnectedAt = DateTime.UtcNow,
            LastActivity = DateTime.UtcNow,
            ServerInstanceId = "test-instance",
        };

        db.ConnectedUsers.Add(user);
        await db.SaveChangesAsync();

        // Act
        await hub.JoinChannel(channel);

        // Assert
        groupManagerMock.Verify(
            g => g.RemoveFromGroupAsync(It.IsAny<string>(), It.IsAny<string>(), default),
            Times.Never);

        groupMock.Verify(
            g => g.SendCoreAsync("UserLeft", It.IsAny<object[]>(), default),
            Times.Never);
    }

    [Fact]
    public async Task LeaveChannel_ShouldRemoveConnectedUser()
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
            ConnectionId = testConnectionId,
            ConnectedAt = DateTime.UtcNow,
            LastActivity = DateTime.UtcNow,
            ServerInstanceId = "test-instance",
        };

        db.ConnectedUsers.Add(user);
        await db.SaveChangesAsync();

        // Act
        await hub.LeaveChannel(channel);

        // Assert
        var updatedUser = await db.ConnectedUsers
            .FirstOrDefaultAsync(u => u.Username == username && u.ConnectionId == testConnectionId);

        Assert.Null(updatedUser);

        groupManagerMock.Verify(
            g => g.RemoveFromGroupAsync(testConnectionId, channel, default),
            Times.Once);

        groupMock.Verify(
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
            ConnectionId = testConnectionId,
            ConnectedAt = DateTime.UtcNow,
            LastActivity = DateTime.UtcNow,
            ServerInstanceId = "test-instance",
        };

        db.ConnectedUsers.Add(user);
        await db.SaveChangesAsync();

        // Act
        await hub.LeaveChannel(wrongChannel);

        // Assert
        var updatedUser = await db.ConnectedUsers
            .FirstOrDefaultAsync(u => u.Username == username);

        Assert.NotNull(updatedUser);
        Assert.Equal(actualChannel, updatedUser!.Channel);

        groupManagerMock.Verify(
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
            IsMuted = false,
        };
        db.Channels.Add(channelEntity);
        var connectorUser = new ConnectedUser
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid().ToString(),
            Username = "sender",
            Channel = channel,
            ConnectionId = testConnectionId,
            ConnectedAt = DateTime.UtcNow,
            LastActivity = DateTime.UtcNow,
            ServerInstanceId = "test-instance",
        };
        db.ConnectedUsers.Add(connectorUser);
        await db.SaveChangesAsync();

        var messageRequest = new SendMessageRequest
        {
            Content = "Hello, World!",
            Channel = channel,
        };

        // Act
        await hub.SendMessage(messageRequest);

        // Assert
        var message = await db.Messages
            .FirstOrDefaultAsync(m => m.UserId == connectorUser.UserId && m.Content == messageRequest.Content);

        Assert.NotNull(message);
        Assert.Equal(messageRequest.Channel, message!.Channel);
        Assert.False(message.IsDeleted);

        groupMock.Verify(
            g => g.SendCoreAsync("ReceiveMessage", It.Is<object[]>(args => args.Length == 1), default),
            Times.Once);
    }

    [Fact]
    public async Task SendMessage_WithConnectedUser_ShouldUpdateLastActivity()
    {
        // Arrange
        var channel = "general";
        var username = "sender";

        db.Channels.Add(new Channel
        {
            Id = Guid.NewGuid(),
            Name = channel,
            CreatedBy = "admin",
            CreatedAt = DateTime.UtcNow,
            IsMuted = false,
        });

        var user = new ConnectedUser
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid().ToString(),
            Username = username,
            Channel = channel,
            ConnectionId = testConnectionId,
            ConnectedAt = DateTime.UtcNow.AddMinutes(-10),
            LastActivity = DateTime.UtcNow.AddMinutes(-5),
            ServerInstanceId = "test-instance",
        };

        db.ConnectedUsers.Add(user);
        await db.SaveChangesAsync();

        var oldActivity = user.LastActivity;

        var messageRequest = new SendMessageRequest
        {
            Content = "Test message",
            Channel = channel,
        };

        // Act
        await Task.Delay(100);
        await hub.SendMessage(messageRequest);

        // Assert
        var updatedUser = await db.ConnectedUsers
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
            IsMuted = true,
        };

        var user = new ConnectedUser
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid().ToString(),
            Username = "user-123",
            Channel = channel,
            ConnectionId = testConnectionId,
            ConnectedAt = DateTime.UtcNow.AddMinutes(-10),
            LastActivity = DateTime.UtcNow.AddMinutes(-5),
            ServerInstanceId = "test-instance",
        };

        db.ConnectedUsers.Add(user);

        db.Channels.Add(mutedChannel);
        await db.SaveChangesAsync();

        var messageRequest = new SendMessageRequest
        {
            Content = "This should not be sent",
            Channel = channel,
        };

        // Act
        await hub.SendMessage(messageRequest);

        // Assert
        var message = await db.Messages
            .FirstOrDefaultAsync(m => m.UserId == user.UserId && m.Content == messageRequest.Content);

        Assert.Null(message);

        callerMock.Verify(
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
            IsMuted = true,
        };

        var user = new ConnectedUser
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid().ToString(),
            Username = creatorUsername,
            Channel = channel,
            ConnectionId = testConnectionId,
            ConnectedAt = DateTime.UtcNow.AddMinutes(-10),
            LastActivity = DateTime.UtcNow.AddMinutes(-5),
            ServerInstanceId = "test-instance",
        };

        db.ConnectedUsers.Add(user);

        db.Channels.Add(mutedChannel);
        await db.SaveChangesAsync();

        var messageRequest = new SendMessageRequest
        {
            Content = "Creator can send",
            Channel = channel,
        };

        // Act
        await hub.SendMessage(messageRequest);

        // Assert
        var message = await db.Messages
            .FirstOrDefaultAsync(m => m.UserId == user.UserId && m.Content == messageRequest.Content);

        Assert.NotNull(message);
        Assert.Equal("Creator can send", message!.Content);

        groupMock.Verify(
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
            LastActivity = DateTime.UtcNow,
            ServerInstanceId = "test-instance",
        };

        db.ConnectedUsers.Add(recipientUser);

        var user = new ConnectedUser
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid().ToString(),
            Username = "sender",
            Channel = "general",
            ConnectionId = testConnectionId,
            ConnectedAt = DateTime.UtcNow.AddMinutes(-10),
            LastActivity = DateTime.UtcNow.AddMinutes(-5),
            ServerInstanceId = "test-instance",
        };

        db.ConnectedUsers.Add(user);

        await db.SaveChangesAsync();

        var messageRequest = new SendPrivateMessageRequest
        {
            RecipientUserId = recipientUser.UserId,
            RecipientUsername = "recipient",
            Content = "Private message",
        };

        // Act
        await hub.SendPrivateMessage(messageRequest);

        // Assert
        var message = await db.PrivateMessages
            .FirstOrDefaultAsync(m =>
                m.SenderUserId == user.UserId &&
                m.RecipientUserId == messageRequest.RecipientUserId);

        Assert.NotNull(message);
        Assert.Equal(messageRequest.Content, message!.Content);
        Assert.False(message.IsRead);

        singleClientMock.Verify(
            c => c.SendCoreAsync("ReceivePrivateMessage", It.Is<object[]>(args => args.Length == 1), default),
            Times.Once);

        callerMock.Verify(
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
            ConnectionId = testConnectionId,
            ConnectedAt = DateTime.UtcNow.AddMinutes(-10),
            LastActivity = DateTime.UtcNow.AddMinutes(-5),
            ServerInstanceId = "test-instance",
        };

        db.ConnectedUsers.Add(user);
        await db.SaveChangesAsync();

        var messageRequest = new SendPrivateMessageRequest
        {
            RecipientUserId = "offline_recipient",
            RecipientUsername = "offline_recipient",
            Content = "Message to offline user",
        };

        // Act
        await hub.SendPrivateMessage(messageRequest);

        // Assert
        var message = await db.PrivateMessages
            .FirstOrDefaultAsync(m => m.SenderUserId == user.UserId && m.RecipientUserId == "offline_recipient");

        Assert.NotNull(message);

        singleClientMock.Verify(
            c => c.SendCoreAsync("ReceivePrivateMessage", It.IsAny<object[]>(), default),
            Times.Never);

        callerMock.Verify(
            c => c.SendCoreAsync("PrivateMessageSent", It.IsAny<object[]>(), default),
            Times.Once);
    }

    [Fact]
    public async Task SendPrivateMessage_WithGlobalyMutedUser_ShouldOnlyNotifySender()
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
            LastActivity = DateTime.UtcNow,
            ServerInstanceId = "test-instance",
        };

        db.ConnectedUsers.Add(recipientUser);

        var user = new ConnectedUser
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid().ToString(),
            Username = "sender",
            Channel = "general",
            ConnectionId = testConnectionId,
            ConnectedAt = DateTime.UtcNow.AddMinutes(-10),
            LastActivity = DateTime.UtcNow.AddMinutes(-5),
            ServerInstanceId = "test-instance",
        };

        db.ConnectedUsers.Add(user);

        var mutedUser = new MutedUser
        {
            Id = Guid.NewGuid(),
            UserId = user.UserId,
            MutedAt = DateTime.UtcNow,
            MutedByUserId = "admin",
            Reason = "Spamming",
        };

        db.MutedUsers.Add(mutedUser);

        await db.SaveChangesAsync();

        var messageRequest = new SendPrivateMessageRequest
        {
            RecipientUserId = recipientUser.UserId,
            RecipientUsername = "recipient",
            Content = "Private message",
        };

        // Act
        await hub.SendPrivateMessage(messageRequest);

        // Assert
        var message = await db.PrivateMessages
            .FirstOrDefaultAsync(m =>
                m.SenderUserId == user.UserId &&
                m.RecipientUserId == messageRequest.RecipientUserId);

        Assert.NotNull(message);
        Assert.Equal(messageRequest.Content, message!.Content);
        Assert.False(message.IsRead);
        Assert.True(message.IsDeletedByRecipient);

        singleClientMock.Verify(
            c => c.SendCoreAsync("ReceivePrivateMessage", It.Is<object[]>(args => args.Length == 1), default),
            Times.Never);

        callerMock.Verify(
            c => c.SendCoreAsync("PrivateMessageSent", It.Is<object[]>(args => args.Length == 1), default),
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
            ConnectionId = testConnectionId,
            ConnectedAt = DateTime.UtcNow,
            LastActivity = DateTime.UtcNow,
            ServerInstanceId = "test-instance",
        };

        var senderUser = new ConnectedUser
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid().ToString(),
            Username = senderUsername,
            Channel = "general",
            ConnectionId = "sender-connection-id",
            ConnectedAt = DateTime.UtcNow,
            LastActivity = DateTime.UtcNow,
            ServerInstanceId = "test-instance",
        };

        db.ConnectedUsers.AddRange(recipientUser, senderUser);

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

        db.PrivateMessages.Add(unreadMessage);
        await db.SaveChangesAsync();

        // Act
        await hub.MarkPrivateMessagesAsRead(senderUser.UserId);

        // Assert
        var message = await db.PrivateMessages.FindAsync(unreadMessage.Id);
        Assert.NotNull(message);
        Assert.True(message!.IsRead);

        singleClientMock.Verify(
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
            ConnectionId = testConnectionId,
            ConnectedAt = DateTime.UtcNow,
            LastActivity = DateTime.UtcNow,
            ServerInstanceId = "test-instance",
        };

        db.ConnectedUsers.Add(user);
        await db.SaveChangesAsync();

        // Act
        await hub.OnDisconnectedAsync(null);

        // Assert
        var removedUser = await db.ConnectedUsers
            .FirstOrDefaultAsync(u => u.ConnectionId == testConnectionId);

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
            ConnectionId = testConnectionId,
            ConnectedAt = DateTime.UtcNow,
            LastActivity = DateTime.UtcNow,
            ServerInstanceId = "test-instance",
        };

        db.ConnectedUsers.Add(user);
        await db.SaveChangesAsync();

        // Act
        await hub.OnDisconnectedAsync(null);

        // Assert
        groupMock.Verify(
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
            ConnectionId = testConnectionId,
            ConnectedAt = DateTime.UtcNow,
            LastActivity = DateTime.UtcNow,
            ServerInstanceId = "test-instance",
        };

        db.ConnectedUsers.Add(user);
        await db.SaveChangesAsync();

        // Act
        await hub.OnDisconnectedAsync(null);

        // Assert
        groupMock.Verify(
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
            ConnectionId = testConnectionId,
            ConnectedAt = DateTime.UtcNow,
            LastActivity = DateTime.UtcNow,
            ServerInstanceId = "test-instance",
        };

        db.ConnectedUsers.Add(user);
        await db.SaveChangesAsync();

        // Act
        var exists = await db.ConnectedUsers
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
            new(ClaimTypes.Name, username),
        };

        var claimsPrincipal = new ClaimsPrincipal(new ClaimsIdentity(claims));

        contextMock.Setup(c => c.ConnectionId).Returns(connectionId);
        contextMock.Setup(c => c.User).Returns(claimsPrincipal);

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
            ServerInstanceId = "server-1",
        };

        db.ConnectedUsers.Add(connectedUser);
        await db.SaveChangesAsync();

        // Act
        await hub.OnDisconnectedAsync(null);

        // Assert
        // Vérifier que l'utilisateur a été supprimé de ConnectedUsers
        var userExists = await db.ConnectedUsers
            .AnyAsync(u => u.ConnectionId == connectionId);
        Assert.False(userExists);

        // Vérifier que la notification offline a été envoyée
        allClientsMock.Verify(
            c => c.SendCoreAsync(
                "UserStatusChanged",
                It.Is<object[]>(o => o.Length == 3 && (string)o[0] == username && !(bool)o[2]),
                default),
            Times.Once);

        // Vérifier que UserLeft a été appelé pour le canal
        groupMock.Verify(
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
            new(ClaimTypes.Name, username),
        };

        var claimsPrincipal = new ClaimsPrincipal(new ClaimsIdentity(claims));

        contextMock.Setup(c => c.ConnectionId).Returns(connectionId1);
        contextMock.Setup(c => c.User).Returns(claimsPrincipal);

        // Ajouter deux connexions pour le même utilisateur
        db.ConnectedUsers.AddRange(
            new ConnectedUser
            {
                Id = Guid.NewGuid(),
                Username = username,
                ConnectionId = connectionId1,
                Channel = "general",
                ConnectedAt = DateTime.UtcNow,
                LastActivity = DateTime.UtcNow,
                ServerInstanceId = "server-1",
            },
            new ConnectedUser
            {
                Id = Guid.NewGuid(),
                Username = username,
                ConnectionId = connectionId2,
                Channel = "random",
                ConnectedAt = DateTime.UtcNow,
                LastActivity = DateTime.UtcNow,
                ServerInstanceId = "server-1",
            });

        await db.SaveChangesAsync();

        // Act
        await hub.OnDisconnectedAsync(null);

        // Assert
        // La notification offline ne doit PAS être envoyée car il reste une autre connexion
        allClientsMock.Verify(
            c => c.SendCoreAsync(
                "UserStatusChanged",
                It.Is<object[]>(o => o.Length == 2 && !(bool)o[1]),
                default),
            Times.Never);

        // Vérifier qu'il reste une connexion
        var remainingConnections = await db.ConnectedUsers
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
            new(ClaimTypes.Name, username),
        };

        var claimsPrincipal = new ClaimsPrincipal(new ClaimsIdentity(claims));

        contextMock.Setup(c => c.ConnectionId).Returns(connectionId);
        contextMock.Setup(c => c.User).Returns(claimsPrincipal);

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
            ServerInstanceId = "server-1",
        };

        db.ConnectedUsers.Add(connectedUser);
        await db.SaveChangesAsync();

        // Act
        await hub.OnDisconnectedAsync(null);

        // Assert
        allClientsMock.Verify(
            c => c.SendCoreAsync(
                "UserStatusChanged",
                It.Is<object[]>(o => o.Length == 3 && (string)o[0] == username && (string)o[1] == connectedUser.UserId && !(bool)o[2]),
                default),
            Times.Once);

        // UserLeft ne doit PAS être appelé car il n'y avait pas de canal
        groupMock.Verify(
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

        contextMock.Setup(c => c.ConnectionId).Returns(connectionId);
        contextMock.Setup(c => c.User).Returns(claimsPrincipal);

        // Act
        await hub.OnConnectedAsync();

        // Assert
        allClientsMock.Verify(
            c => c.SendCoreAsync(
                "UserStatusChanged",
                It.IsAny<object[]>(),
                default),
            Times.Never);
    }

    [Fact]
    public async Task SendMessage_WithMutedUser_ShouldSaveButNotBroadcastOnlyToCaller()
    {
        // Arrange
        var channel = "general";

        var channelEntity = new Channel
        {
            Id = Guid.NewGuid(),
            Name = channel,
            CreatedBy = "admin",
            CreatedAt = DateTime.UtcNow,
            IsMuted = false,
        };
        db.Channels.Add(channelEntity);

        var connectedUser = new ConnectedUser
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid().ToString(),
            Username = "muteduser",
            Channel = channel,
            ConnectionId = testConnectionId,
            ConnectedAt = DateTime.UtcNow,
            LastActivity = DateTime.UtcNow,
            ServerInstanceId = "test-instance",
        };
        db.ConnectedUsers.Add(connectedUser);

        // Muter l'utilisateur
        var mute = new MutedUser
        {
            Id = Guid.NewGuid(),
            ChannelName = channel,
            UserId = connectedUser.UserId,
            MutedByUserId = "admin-id",
            MutedAt = DateTime.UtcNow,
            Reason = "Spam",
        };
        db.MutedUsers.Add(mute);
        await db.SaveChangesAsync();

        var messageRequest = new SendMessageRequest
        {
            Content = "This message should be saved but not broadcast",
            Channel = channel,
        };

        // Act
        await hub.SendMessage(messageRequest);

        // Assert
        // Vérifier que le message est bien sauvegardé en BDD
        var message = await db.Messages
            .FirstOrDefaultAsync(m => m.UserId == connectedUser.UserId && m.Content == messageRequest.Content);

        Assert.NotNull(message);
        Assert.Equal(messageRequest.Channel, message!.Channel);
        Assert.True(message.IsDeleted);

        // Vérifier qu'AUCUN message n'a été envoyé au groupe
        groupMock.Verify(
            g => g.SendCoreAsync("ReceiveMessage", It.IsAny<object[]>(), default),
            Times.Never);

        // Vérifier qu'UN message a été envoyé à l'appelant
        callerMock.Verify(
            c => c.SendCoreAsync("ReceiveMessage", It.IsAny<object[]>(), default),
            Times.Once);
    }

    [Fact]
    public async Task SendMessage_WithGlobalyMutedUser_ShouldSaveButNotBroadcastOnlyToCaller()
    {
        // Arrange
        var channel = "general";

        var channelEntity = new Channel
        {
            Id = Guid.NewGuid(),
            Name = channel,
            CreatedBy = "admin",
            CreatedAt = DateTime.UtcNow,
            IsMuted = false,
        };
        db.Channels.Add(channelEntity);

        var connectedUser = new ConnectedUser
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid().ToString(),
            Username = "muteduser",
            Channel = channel,
            ConnectionId = testConnectionId,
            ConnectedAt = DateTime.UtcNow,
            LastActivity = DateTime.UtcNow,
            ServerInstanceId = "test-instance",
        };
        db.ConnectedUsers.Add(connectedUser);

        // Muter l'utilisateur
        var mute = new MutedUser
        {
            Id = Guid.NewGuid(),
            UserId = connectedUser.UserId,
            MutedByUserId = "admin-id",
            MutedAt = DateTime.UtcNow,
            Reason = "Spam",
        };
        db.MutedUsers.Add(mute);
        await db.SaveChangesAsync();

        var messageRequest = new SendMessageRequest
        {
            Content = "This message should be saved but not broadcast",
            Channel = channel,
        };

        // Act
        await hub.SendMessage(messageRequest);

        // Assert
        // Vérifier que le message est bien sauvegardé en BDD
        var message = await db.Messages
            .FirstOrDefaultAsync(m => m.UserId == connectedUser.UserId && m.Content == messageRequest.Content);

        Assert.NotNull(message);
        Assert.Equal(messageRequest.Channel, message!.Channel);
        Assert.True(message.IsDeleted);

        // Vérifier qu'AUCUN message n'a été envoyé au groupe
        groupMock.Verify(
            g => g.SendCoreAsync("ReceiveMessage", It.IsAny<object[]>(), default),
            Times.Never);

        // Vérifier qu'UN message a été envoyé à l'appelant
        callerMock.Verify(
            c => c.SendCoreAsync("ReceiveMessage", It.IsAny<object[]>(), default),
            Times.Once);
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
            IsMuted = false,
        };
        db.Channels.Add(channelEntity);

        var connectedUser = new ConnectedUser
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid().ToString(),
            Username = "normaluser",
            Channel = channel,
            ConnectionId = testConnectionId,
            ConnectedAt = DateTime.UtcNow,
            LastActivity = DateTime.UtcNow,
            ServerInstanceId = "test-instance",
        };
        db.ConnectedUsers.Add(connectedUser);
        await db.SaveChangesAsync();

        var messageRequest = new SendMessageRequest
        {
            Content = "Normal message",
            Channel = channel,
        };

        // Act
        await hub.SendMessage(messageRequest);

        // Assert
        // Message sauvegardé
        var message = await db.Messages
            .FirstOrDefaultAsync(m => m.UserId == connectedUser.UserId);
        Assert.NotNull(message);

        // Message diffusé au groupe
        groupMock.Verify(
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
            IsMuted = false,
        };
        db.Channels.Add(channelEntity);

        var connectedUser = new ConnectedUser
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid().ToString(),
            Username = "testuser",
            Channel = channel,
            ConnectionId = testConnectionId,
            ConnectedAt = DateTime.UtcNow,
            LastActivity = DateTime.UtcNow,
            ServerInstanceId = "test-instance",
        };
        db.ConnectedUsers.Add(connectedUser);

        // Muter puis démuter
        var mute = new MutedUser
        {
            Id = Guid.NewGuid(),
            ChannelName = channel,
            UserId = connectedUser.UserId,
            MutedByUserId = "admin-id",
            MutedAt = DateTime.UtcNow,
        };
        db.MutedUsers.Add(mute);
        await db.SaveChangesAsync();

        // Premier message (mute)
        var firstMessage = new SendMessageRequest
        {
            Content = "First message while muted",
            Channel = channel,
        };
        await hub.SendMessage(firstMessage);

        // Vérifier pas de broadcast
        groupMock.Verify(
            g => g.SendCoreAsync("ReceiveMessage", It.IsAny<object[]>(), default),
            Times.Never);

        // Démuter
        db.MutedUsers.Remove(mute);
        await db.SaveChangesAsync();

        // Deuxième message (non mute)
        var secondMessage = new SendMessageRequest
        {
            Content = "Second message after unmute",
            Channel = channel,
        };
        await hub.SendMessage(secondMessage);

        // Assert
        // Les deux messages sont en BDD
        var messages = await db.Messages
            .Where(m => m.UserId == connectedUser.UserId)
            .ToListAsync();
        Assert.Equal(2, messages.Count);

        // Le deuxième message a été diffusé
        groupMock.Verify(
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
            IsMuted = true,
        };
        db.Channels.Add(mutedChannel);

        var adminUser = new ReservedUsername
        {
            Id = Guid.NewGuid(),
            Username = "admin",
            Email = "admin@test.com",
            Provider = ExternalAuthProvider.Google,
            ExternalUserId = "ext-admin",
            CreatedAt = DateTime.UtcNow,
            LastLoginAt = DateTime.UtcNow,
            IsAdmin = true,
        };
        db.ReservedUsernames.Add(adminUser);

        var connectedUser = new ConnectedUser
        {
            Id = Guid.NewGuid(),
            UserId = adminUser.Id.ToString(),
            Username = adminUser.Username,
            Channel = channel,
            ConnectionId = testConnectionId,
            ConnectedAt = DateTime.UtcNow,
            LastActivity = DateTime.UtcNow,
            ServerInstanceId = "test-instance",
        };
        db.ConnectedUsers.Add(connectedUser);

        // Admin mute individuellement (rare mais possible)
        var mute = new MutedUser
        {
            Id = Guid.NewGuid(),
            ChannelName = channel,
            UserId = connectedUser.UserId,
            MutedByUserId = "creator-id",
            MutedAt = DateTime.UtcNow,
        };
        db.MutedUsers.Add(mute);
        await db.SaveChangesAsync();

        var messageRequest = new SendMessageRequest
        {
            Content = "Admin message in muted channel",
            Channel = channel,
        };

        // Act
        await hub.SendMessage(messageRequest);

        // Assert
        // Message sauvegardé
        var message = await db.Messages
            .FirstOrDefaultAsync(m => m.UserId == connectedUser.UserId);
        Assert.NotNull(message);

        // Pas de broadcast car l'utilisateur est mute individuellement
        // (le mute individuel prend le dessus sur le statut admin)
        groupMock.Verify(
            g => g.SendCoreAsync("ReceiveMessage", It.IsAny<object[]>(), default),
            Times.Never);
    }

    public async ValueTask DisposeAsync()
    {
        await db.DisposeAsync();
        GC.SuppressFinalize(this);
    }
}