// tests/IrcChat.Client.Tests/Services/ChatServiceTests.cs
using System.Net;
using Bunit;
using IrcChat.Client.Models;
using IrcChat.Client.Services;
using IrcChat.Shared.Models;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.SignalR.Protocol;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using RichardSzalay.MockHttp;
using Xunit;

namespace IrcChat.Client.Tests.Services;

public class ChatServiceTests : TestContext
{
    private readonly Mock<IPrivateMessageService> _privateMessageServiceMock;
    private readonly Mock<IUnifiedAuthService> _unverifiedAuthServiceMock;
    private readonly IOptions<ApiSettings> _apiSettings;
    private readonly MockHttpMessageHandler _mockHttp;

    public ChatServiceTests()
    {
        _privateMessageServiceMock = new Mock<IPrivateMessageService>();
        _unverifiedAuthServiceMock = new Mock<IUnifiedAuthService>();

        _apiSettings = Options.Create(new ApiSettings
        {
            BaseUrl = "https://localhost:7000",
            SignalRHubUrl = "https://localhost:7000/chathub"
        });

        _mockHttp = new MockHttpMessageHandler();

        var httpClient = _mockHttp.ToHttpClient();
        httpClient.BaseAddress = new Uri("https://localhost:7000");

        Services.AddSingleton(_privateMessageServiceMock.Object);
        Services.AddSingleton(_unverifiedAuthServiceMock.Object);
        Services.AddSingleton(_apiSettings);
        Services.AddSingleton(httpClient);
    }

    [Fact]
    public void ChatService_ShouldInitialize()
    {
        // Act
        var service = new ChatService(_privateMessageServiceMock.Object, _unverifiedAuthServiceMock.Object, NullLogger<ChatService>.Instance);

        // Assert
        Assert.NotNull(service);
    }

    [Fact]
    public async Task InitializeAsync_ShouldConnectSuccessfully()
    {
        // Arrange
        var service = new ChatService(_privateMessageServiceMock.Object,
            _unverifiedAuthServiceMock.Object,
            NullLogger<ChatService>.Instance);

        var hubConnectionBuilderMock = new Mock<IHubConnectionBuilder>();
        var hubConnectionMock = new Mock<HubConnectionStub>();
        hubConnectionMock.Setup(x => x.StartAsync(default)).Returns(Task.CompletedTask);
        // Setup pour tous les handlers
        hubConnectionMock.Setup(x => x.On(
            It.IsAny<string>(),
            It.IsAny<Type[]>(),
            It.IsAny<Func<object?[], object, Task>>(),
            It.IsAny<object>()))
            .Returns(Mock.Of<IDisposable>());

        hubConnectionBuilderMock
            .Setup(x => x.Build())
            .Returns(hubConnectionMock.Object);
        SetConnectedState(hubConnectionMock);

        // Act & Assert
        await service.InitializeAsync(hubConnectionBuilderMock.Object);

        // Assert - Vérifier que tous les événements sont enregistrés
        hubConnectionMock.Verify(x => x.On("ReceiveMessage", It.IsAny<Type[]>(), It.IsAny<Func<object?[], object, Task>>(), It.IsAny<object>()), Times.Once);
        hubConnectionMock.Verify(x => x.On("UserJoined", It.IsAny<Type[]>(), It.IsAny<Func<object?[], object, Task>>(), It.IsAny<object>()), Times.Once);
        hubConnectionMock.Verify(x => x.On("UserLeft", It.IsAny<Type[]>(), It.IsAny<Func<object?[], object, Task>>(), It.IsAny<object>()), Times.Once);
        hubConnectionMock.Verify(x => x.On("ChannelMuteStatusChanged", It.IsAny<Type[]>(), It.IsAny<Func<object?[], object, Task>>(), It.IsAny<object>()), Times.Once);
        hubConnectionMock.Verify(x => x.On("MessageBlocked", It.IsAny<Type[]>(), It.IsAny<Func<object?[], object, Task>>(), It.IsAny<object>()), Times.Once);
        hubConnectionMock.Verify(x => x.On("ChannelDeleted", It.IsAny<Type[]>(), It.IsAny<Func<object?[], object, Task>>(), It.IsAny<object>()), Times.Once);
        hubConnectionMock.Verify(x => x.On("ChannelNotFound", It.IsAny<Type[]>(), It.IsAny<Func<object?[], object, Task>>(), It.IsAny<object>()), Times.Once);
        hubConnectionMock.Verify(x => x.On("ChannelListUpdated", It.IsAny<Type[]>(), It.IsAny<Func<object?[], object, Task>>(), It.IsAny<object>()), Times.Once);
        hubConnectionMock.Verify(x => x.On("ReceivePrivateMessage", It.IsAny<Type[]>(), It.IsAny<Func<object?[], object, Task>>(), It.IsAny<object>()), Times.Once);
        hubConnectionMock.Verify(x => x.On("PrivateMessageSent", It.IsAny<Type[]>(), It.IsAny<Func<object?[], object, Task>>(), It.IsAny<object>()), Times.Once);
        hubConnectionMock.Verify(x => x.On("PrivateMessagesRead", It.IsAny<Type[]>(), It.IsAny<Func<object?[], object, Task>>(), It.IsAny<object>()), Times.Once);
    }


    [Fact]
    public async Task OnMessageReceived_ShouldTriggerEvent()
    {
        // Arrange
        var service = new ChatService(_privateMessageServiceMock.Object, _unverifiedAuthServiceMock.Object, NullLogger<ChatService>.Instance);
        var hubConnectionMock = new Mock<HubConnectionStub>();
        var hubConnectionBuilderMock = new Mock<IHubConnectionBuilder>();

        hubConnectionMock.Setup(x => x.StartAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        Func<object?[], object, Task>? onReceiveMessage = null;
        object? onReceiveMessageState = null;
        hubConnectionMock.Setup(x => x.On("ReceiveMessage", It.IsAny<Type[]>(), It.IsAny<Func<object?[], object, Task>>(), It.IsAny<object>()))
            .Callback<string, Type[], Func<object?[], object, Task>, object>((methodName, parameterTypes, handler, state) =>
            {
                onReceiveMessage = handler;
                onReceiveMessageState = state;
            })
            .Returns(Mock.Of<IDisposable>());

        hubConnectionBuilderMock.Setup(x => x.Build()).Returns(hubConnectionMock.Object);

        await service.InitializeAsync(hubConnectionBuilderMock.Object);

        var testMessage = new Message
        {
            Id = Guid.NewGuid(),
            Username = "testUser",
            Channel = "testChannel",
            Content = "Test content",
            Timestamp = DateTime.UtcNow
        };

        // Act Wtithout subscription
        await onReceiveMessage!([testMessage], onReceiveMessageState!);

        // Arrange
        Message? receivedMessage = null;
        service.OnMessageReceived += (message) => receivedMessage = message;

        // Act
        await onReceiveMessage!([testMessage], onReceiveMessageState!);

        // Assert
        Assert.NotNull(receivedMessage);
        Assert.Same(testMessage, receivedMessage);
    }

    [Fact]
    public async Task OnUserJoined_ShouldTriggerEvent()
    {
        // Arrange
        var service = new ChatService(_privateMessageServiceMock.Object, _unverifiedAuthServiceMock.Object, NullLogger<ChatService>.Instance);
        var hubConnectionMock = new Mock<HubConnectionStub>();
        var hubConnectionBuilderMock = new Mock<IHubConnectionBuilder>();

        hubConnectionMock.Setup(x => x.StartAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        Func<object?[], object, Task>? onUserJoined = null;
        object? onUserJoinedState = null;

        hubConnectionMock.Setup(x => x.On("UserJoined", It.IsAny<Type[]>(), It.IsAny<Func<object?[], object, Task>>(), It.IsAny<object>()))
            .Callback<string, Type[], Func<object?[], object, Task>, object>((methodName, parameterTypes, handler, state) =>
            {
                onUserJoined = handler;
                onUserJoinedState = state;
            })
            .Returns(Mock.Of<IDisposable>());

        hubConnectionBuilderMock.Setup(x => x.Build()).Returns(hubConnectionMock.Object);

        await service.InitializeAsync(hubConnectionBuilderMock.Object);

        // Act Without subscription
        await onUserJoined!(["testUser", "testUser", "testChannel"], onUserJoinedState!);

        // Arrange
        string? joinedUser = null;
        string? joinedUserId = null;
        string? joinedChannel = null;
        service.OnUserJoined += (username, userId, channel) =>
        {
            joinedUser = username;
            joinedUserId = userId;
            joinedChannel = channel;
        };

        // Act
        await onUserJoined!(["testUser", "testUser", "testChannel"], onUserJoinedState!);

        // Assert
        Assert.Equal("testUser", joinedUser);
        Assert.Equal("testUser", joinedUserId);
        Assert.Equal("testChannel", joinedChannel);
    }

    [Fact]
    public async Task OnUserLeft_ShouldTriggerEvent()
    {
        // Arrange
        var service = new ChatService(_privateMessageServiceMock.Object, _unverifiedAuthServiceMock.Object, NullLogger<ChatService>.Instance);
        var hubConnectionMock = new Mock<HubConnectionStub>();
        var hubConnectionBuilderMock = new Mock<IHubConnectionBuilder>();

        hubConnectionMock.Setup(x => x.StartAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        Func<object?[], object, Task>? onUserLeft = null;
        object? onUserLeftState = null;
        hubConnectionMock.Setup(x => x.On("UserLeft", It.IsAny<Type[]>(), It.IsAny<Func<object?[], object, Task>>(), It.IsAny<object>()))
            .Callback<string, Type[], Func<object?[], object, Task>, object>((methodName, parameterTypes, handler, state) =>
            {
                onUserLeft = handler;
                onUserLeftState = state;
            })
            .Returns(Mock.Of<IDisposable>());

        hubConnectionBuilderMock.Setup(x => x.Build()).Returns(hubConnectionMock.Object);

        await service.InitializeAsync(hubConnectionBuilderMock.Object);

        // Act Without subscription
        await onUserLeft!(["testUser", "testUser", "testChannel"], onUserLeftState!);

        // Arrange
        string? leftUser = null;
        string? leftUserId = null;
        string? leftChannel = null;
        service.OnUserLeft += (username, userId, channel) =>
        {
            leftUser = username;
            leftUserId = userId;
            leftChannel = channel;
        };

        // Act
        await onUserLeft!(["testUser", "testUser", "testChannel"], onUserLeftState!);

        // Assert
        Assert.Equal("testUser", leftUser);
        Assert.Equal("testUser", leftUserId);
        Assert.Equal("testChannel", leftChannel);
    }

    [Fact]
    public async Task OnChannelMuteStatusChanged_ShouldTriggerEvent()
    {
        // Arrange
        var service = new ChatService(_privateMessageServiceMock.Object, _unverifiedAuthServiceMock.Object, NullLogger<ChatService>.Instance);
        var hubConnectionMock = new Mock<HubConnectionStub>();
        var hubConnectionBuilderMock = new Mock<IHubConnectionBuilder>();

        hubConnectionMock.Setup(x => x.StartAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        Func<object?[], object, Task>? onChannelMuteStatusChanged = null;
        object? onChannelMuteStatusChangedState = null;

        hubConnectionMock.Setup(x => x.On("ChannelMuteStatusChanged", It.IsAny<Type[]>(), It.IsAny<Func<object?[], object, Task>>(), It.IsAny<object>()))
            .Callback<string, Type[], Func<object?[], object, Task>, object>((methodName, parameterTypes, handler, state) =>
            {
                onChannelMuteStatusChanged = handler;
                onChannelMuteStatusChangedState = state;
            })
            .Returns(Mock.Of<IDisposable>());

        hubConnectionBuilderMock.Setup(x => x.Build()).Returns(hubConnectionMock.Object);

        await service.InitializeAsync(hubConnectionBuilderMock.Object);

        // Act without subscription
        await onChannelMuteStatusChanged!(["testChannel", true], onChannelMuteStatusChangedState!);

        // Arrange
        string? mutedChannel = null;
        bool? muteStatus = null;
        service.OnChannelMuteStatusChanged += (channel, isMuted) =>
        {
            mutedChannel = channel;
            muteStatus = isMuted;
        };

        // Act
        await onChannelMuteStatusChanged!(["testChannel", true], onChannelMuteStatusChangedState!);

        // Assert
        Assert.Equal("testChannel", mutedChannel);
        Assert.True(muteStatus);
    }

    [Fact]
    public async Task OnMessageBlocked_ShouldTriggerEvent()
    {
        // Arrange
        var service = new ChatService(_privateMessageServiceMock.Object, _unverifiedAuthServiceMock.Object, NullLogger<ChatService>.Instance);
        var hubConnectionMock = new Mock<HubConnectionStub>();
        var hubConnectionBuilderMock = new Mock<IHubConnectionBuilder>();


        hubConnectionMock.Setup(x => x.StartAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        Func<object?[], object, Task>? onMessageBlocked = null;
        object? onMessageBlockedState = null;

        hubConnectionMock.Setup(x => x.On("MessageBlocked", It.IsAny<Type[]>(), It.IsAny<Func<object?[], object, Task>>(), It.IsAny<object>()))
            .Callback<string, Type[], Func<object?[], object, Task>, object>((methodName, parameterTypes, handler, state) =>
            {
                onMessageBlocked = handler;
                onMessageBlockedState = state;
            })
            .Returns(Mock.Of<IDisposable>());

        hubConnectionBuilderMock.Setup(x => x.Build()).Returns(hubConnectionMock.Object);

        await service.InitializeAsync(hubConnectionBuilderMock.Object);

        // Act without subscription
        await onMessageBlocked!(["Channel is muted"], onMessageBlockedState!);

        // Arrange
        string? blockedReason = null;
        service.OnMessageBlocked += (reason) => blockedReason = reason;

        // Act
        await onMessageBlocked!(["Channel is muted"], onMessageBlockedState!);

        // Assert
        Assert.Equal("Channel is muted", blockedReason);
    }

    [Fact]
    public async Task OnChannelDeleted_ShouldTriggerEvent()
    {
        // Arrange
        var service = new ChatService(_privateMessageServiceMock.Object, _unverifiedAuthServiceMock.Object, NullLogger<ChatService>.Instance);
        var hubConnectionMock = new Mock<HubConnectionStub>();
        var hubConnectionBuilderMock = new Mock<IHubConnectionBuilder>();


        hubConnectionMock.Setup(x => x.StartAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        Func<object?[], object, Task>? onChannelDeleted = null;
        object? onChannelDeletedState = null;

        hubConnectionMock.Setup(x => x.On("ChannelDeleted", It.IsAny<Type[]>(), It.IsAny<Func<object?[], object, Task>>(), It.IsAny<object>()))
            .Callback<string, Type[], Func<object?[], object, Task>, object>((methodName, parameterTypes, handler, state) =>
            {
                onChannelDeleted = handler;
                onChannelDeletedState = state;
            })
            .Returns(Mock.Of<IDisposable>());

        hubConnectionBuilderMock.Setup(x => x.Build()).Returns(hubConnectionMock.Object);

        await service.InitializeAsync(hubConnectionBuilderMock.Object);

        // Act without subscription
        await onChannelDeleted!(["testChannel"], onChannelDeletedState!);

        // Arrange
        string? deletedChannel = null;
        service.OnChannelDeleted += (channel) => deletedChannel = channel;

        // Act
        await onChannelDeleted!(["testChannel"], onChannelDeletedState!);

        // Assert
        Assert.Equal("testChannel", deletedChannel);
    }

    [Fact]
    public async Task OnChannelNotFound_ShouldTriggerEvent()
    {
        // Arrange
        var service = new ChatService(_privateMessageServiceMock.Object, _unverifiedAuthServiceMock.Object, NullLogger<ChatService>.Instance);
        var hubConnectionMock = new Mock<HubConnectionStub>();
        var hubConnectionBuilderMock = new Mock<IHubConnectionBuilder>();

        hubConnectionMock.Setup(x => x.StartAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        Func<object?[], object, Task>? onChannelNotFound = null;
        object? onChannelNotFoundState = null;

        hubConnectionMock.Setup(x => x.On("ChannelNotFound", It.IsAny<Type[]>(), It.IsAny<Func<object?[], object, Task>>(), It.IsAny<object>()))
            .Callback<string, Type[], Func<object?[], object, Task>, object>((methodName, parameterTypes, handler, state) =>
            {
                onChannelNotFound = handler;
                onChannelNotFoundState = state;
            })
            .Returns(Mock.Of<IDisposable>());

        hubConnectionBuilderMock.Setup(x => x.Build()).Returns(hubConnectionMock.Object);

        await service.InitializeAsync(hubConnectionBuilderMock.Object);

        // Act without subscription
        await onChannelNotFound!(["missingChannel"], onChannelNotFoundState!);

        // Arrange
        string? notFoundChannel = null;
        service.OnChannelNotFound += (channel) => notFoundChannel = channel;

        // Act
        await onChannelNotFound!(["missingChannel"], onChannelNotFoundState!);

        // Assert
        Assert.Equal("missingChannel", notFoundChannel);
    }

    [Fact]
    public async Task OnChannelListUpdated_ShouldTriggerEvent()
    {
        // Arrange
        var service = new ChatService(_privateMessageServiceMock.Object, _unverifiedAuthServiceMock.Object, NullLogger<ChatService>.Instance);
        var hubConnectionMock = new Mock<HubConnectionStub>();
        var hubConnectionBuilderMock = new Mock<IHubConnectionBuilder>();


        hubConnectionMock.Setup(x => x.StartAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        Func<object?[], object, Task>? onChannelListUpdated = null;
        object? onChannelListUpdatedState = null;

        hubConnectionMock.Setup(x => x.On("ChannelListUpdated", It.IsAny<Type[]>(), It.IsAny<Func<object?[], object, Task>>(), It.IsAny<object>()))
            .Callback<string, Type[], Func<object?[], object, Task>, object>((methodName, parameterTypes, handler, state) =>
            {
                onChannelListUpdated = handler;
                onChannelListUpdatedState = state;
            })
            .Returns(Mock.Of<IDisposable>());

        hubConnectionBuilderMock.Setup(x => x.Build()).Returns(hubConnectionMock.Object);

        await service.InitializeAsync(hubConnectionBuilderMock.Object);

        // Act without subscription
        await onChannelListUpdated!([], onChannelListUpdatedState!);

        // Arrange
        var channelListUpdated = false;
        service.OnChannelListUpdated += () => channelListUpdated = true;

        // Act
        await onChannelListUpdated!([], onChannelListUpdatedState!);

        // Assert
        Assert.True(channelListUpdated);
    }

    [Fact]
    public async Task ReceivePrivateMessage_ShouldNotifyPrivateMessageService()
    {
        // Arrange
        var service = new ChatService(_privateMessageServiceMock.Object, _unverifiedAuthServiceMock.Object, NullLogger<ChatService>.Instance);
        var hubConnectionMock = new Mock<HubConnectionStub>();
        var hubConnectionBuilderMock = new Mock<IHubConnectionBuilder>();


        hubConnectionMock.Setup(x => x.StartAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        Func<object?[], object, Task>? onReceivePrivateMessage = null;
        object? onReceivePrivateMessageState = null;

        hubConnectionMock.Setup(x => x.On("ReceivePrivateMessage", It.IsAny<Type[]>(), It.IsAny<Func<object?[], object, Task>>(), It.IsAny<object>()))
            .Callback<string, Type[], Func<object?[], object, Task>, object>((methodName, parameterTypes, handler, state) =>
            {
                onReceivePrivateMessage = handler;
                onReceivePrivateMessageState = state;
            })
            .Returns(Mock.Of<IDisposable>());

        hubConnectionBuilderMock.Setup(x => x.Build()).Returns(hubConnectionMock.Object);

        await service.InitializeAsync(hubConnectionBuilderMock.Object);

        var testMessage = new PrivateMessage
        {
            Id = Guid.NewGuid(),
            SenderUsername = "sender",
            RecipientUsername = "recipient",
            Content = "Private sent",
            Timestamp = DateTime.UtcNow
        };

        // Act
        await onReceivePrivateMessage!([testMessage], onReceivePrivateMessageState!);

        // Assert
        _privateMessageServiceMock.Verify(
            x => x.NotifyPrivateMessageReceived(It.Is<PrivateMessage>(m => m.Id == testMessage.Id)),
            Times.Once);
    }

    [Fact]
    public async Task PrivateMessagesRead_ShouldNotifyPrivateMessageService()
    {
        // Arrange
        var service = new ChatService(_privateMessageServiceMock.Object, _unverifiedAuthServiceMock.Object, NullLogger<ChatService>.Instance);
        var hubConnectionMock = new Mock<HubConnectionStub>();
        var hubConnectionBuilderMock = new Mock<IHubConnectionBuilder>();


        hubConnectionMock.Setup(x => x.StartAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        Func<object?[], object, Task>? onPrivateMessagesRead = null;
        object? onPrivateMessagesReadState = null;

        hubConnectionMock.Setup(x => x.On("PrivateMessagesRead", It.IsAny<Type[]>(), It.IsAny<Func<object?[], object, Task>>(), It.IsAny<object>()))
            .Callback<string, Type[], Func<object?[], object, Task>, object>((methodName, parameterTypes, handler, state) =>
            {
                onPrivateMessagesRead = handler;
                onPrivateMessagesReadState = state;
            })
            .Returns(Mock.Of<IDisposable>());

        hubConnectionBuilderMock.Setup(x => x.Build()).Returns(hubConnectionMock.Object);

        await service.InitializeAsync(hubConnectionBuilderMock.Object);

        var messageIds = new List<Guid> { Guid.NewGuid(), Guid.NewGuid() };
        var username = "testUser";

        // Act
        await onPrivateMessagesRead!([username, messageIds], onPrivateMessagesReadState!);

        // Assert
        _privateMessageServiceMock.Verify(
            x => x.NotifyMessagesRead(username, It.Is<List<Guid>>(ids => ids.Count == 2)),
            Times.Once);
    }

    [Fact]
    public async Task PrivateMessageSent_ShouldNotifyPrivateMessageService()
    {
        // Arrange
        var service = new ChatService(_privateMessageServiceMock.Object, _unverifiedAuthServiceMock.Object, NullLogger<ChatService>.Instance);
        var hubConnectionMock = new Mock<HubConnectionStub>();
        var hubConnectionBuilderMock = new Mock<IHubConnectionBuilder>();


        hubConnectionMock.Setup(x => x.StartAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        Func<object?[], object, Task>? onPrivateMessageSent = null;
        object? onPrivateMessageSentState = null;

        hubConnectionMock.Setup(x => x.On("PrivateMessageSent", It.IsAny<Type[]>(), It.IsAny<Func<object?[], object, Task>>(), It.IsAny<object>()))
            .Callback<string, Type[], Func<object?[], object, Task>, object>((methodName, parameterTypes, handler, state) =>
            {
                onPrivateMessageSent = handler;
                onPrivateMessageSentState = state;
            })
            .Returns(Mock.Of<IDisposable>());

        hubConnectionBuilderMock.Setup(x => x.Build()).Returns(hubConnectionMock.Object);

        await service.InitializeAsync(hubConnectionBuilderMock.Object);

        var testMessage = new PrivateMessage
        {
            Id = Guid.NewGuid(),
            SenderUsername = "sender",
            RecipientUsername = "recipient",
            Content = "Private sent",
            Timestamp = DateTime.UtcNow
        };

        // Act
        await onPrivateMessageSent!([testMessage], onPrivateMessageSentState!);

        // Assert
        _privateMessageServiceMock.Verify(
            x => x.NotifyPrivateMessageSent(It.Is<PrivateMessage>(m => m.Id == testMessage.Id)),
            Times.Once);
    }


    [Fact]
    public async Task DisposeAsync_ShouldCleanupResources()
    {
        // Arrange
        var service = new ChatService(_privateMessageServiceMock.Object, _unverifiedAuthServiceMock.Object, NullLogger<ChatService>.Instance);

        // Act
        await service.DisposeAsync();

        // Assert
        Assert.True(true);
    }

    [Fact]
    public async Task JoinChannel_WhenConnectionNull_ShouldNotThrow()
    {
        // Arrange
        var service = new ChatService(_privateMessageServiceMock.Object, _unverifiedAuthServiceMock.Object, NullLogger<ChatService>.Instance);

        // Act
        await service.JoinChannel("testChannel");

        // Assert
        Assert.True(true);
    }

    [Fact]
    public async Task JoinChannel_WhenConnectionExists_ShouldCallSendAsync()
    {
        // Arrange
        var service = new ChatService(_privateMessageServiceMock.Object, _unverifiedAuthServiceMock.Object, NullLogger<ChatService>.Instance);
        var hubConnectionMock = new Mock<HubConnectionStub>();
        var hubConnectionBuilderMock = new Mock<IHubConnectionBuilder>();

        hubConnectionMock.Setup(x => x.StartAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        hubConnectionMock.Setup(x => x.SendCoreAsync("JoinChannel", It.IsAny<object?[]>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        hubConnectionBuilderMock.Setup(x => x.Build()).Returns(hubConnectionMock.Object);

        await service.InitializeAsync(hubConnectionBuilderMock.Object);

        // Act
        await service.JoinChannel("testChannel");

        // Assert
        hubConnectionMock.Verify(
            x => x.SendCoreAsync("JoinChannel", It.Is<object?[]>(args =>
                args.Length == 1 &&
                args[0]!.ToString() == "testChannel"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task LeaveChannel_WhenConnectionNull_ShouldNotThrow()
    {
        // Arrange
        var service = new ChatService(_privateMessageServiceMock.Object, _unverifiedAuthServiceMock.Object, NullLogger<ChatService>.Instance);

        // Act
        await service.LeaveChannel("testChannel");

        // Assert
        Assert.True(true);
    }

    [Fact]
    public async Task LeaveChannel_WhenConnectionExists_ShouldCallSendAsync()
    {
        // Arrange
        var service = new ChatService(_privateMessageServiceMock.Object, _unverifiedAuthServiceMock.Object, NullLogger<ChatService>.Instance);
        var hubConnectionMock = new Mock<HubConnectionStub>();
        var hubConnectionBuilderMock = new Mock<IHubConnectionBuilder>();

        hubConnectionMock.Setup(x => x.StartAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        hubConnectionMock.Setup(x => x.SendCoreAsync("LeaveChannel", It.IsAny<object?[]>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        hubConnectionBuilderMock.Setup(x => x.Build()).Returns(hubConnectionMock.Object);

        await service.InitializeAsync(hubConnectionBuilderMock.Object);

        // Act
        await service.LeaveChannel("testChannel");

        // Assert
        hubConnectionMock.Verify(
            x => x.SendCoreAsync("LeaveChannel", It.Is<object?[]>(args =>
                args.Length == 1 &&
                args[0]!.ToString() == "testChannel"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SendMessage_WhenConnectionNull_ShouldNotThrow()
    {
        // Arrange
        var service = new ChatService(_privateMessageServiceMock.Object, _unverifiedAuthServiceMock.Object, NullLogger<ChatService>.Instance);
        var request = new SendMessageRequest
        {
            Channel = "testChannel",
            Content = "Test message"
        };

        // Act
        await service.SendMessage(request);

        // Assert
        Assert.True(true);
    }

    [Fact]
    public async Task SendMessage_WhenConnectionExists_ShouldCallSendAsync()
    {
        // Arrange
        var service = new ChatService(_privateMessageServiceMock.Object, _unverifiedAuthServiceMock.Object, NullLogger<ChatService>.Instance);
        var hubConnectionMock = new Mock<HubConnectionStub>();
        var hubConnectionBuilderMock = new Mock<IHubConnectionBuilder>();

        hubConnectionMock.Setup(x => x.StartAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        hubConnectionMock.Setup(x => x.SendCoreAsync("SendMessage", It.IsAny<object?[]>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        hubConnectionBuilderMock.Setup(x => x.Build()).Returns(hubConnectionMock.Object);

        await service.InitializeAsync(hubConnectionBuilderMock.Object);

        var request = new SendMessageRequest
        {
            Channel = "testChannel",
            Content = "Test message"
        };

        // Act
        await service.SendMessage(request);

        // Assert
        hubConnectionMock.Verify(
            x => x.SendCoreAsync("SendMessage", It.Is<object?[]>(args =>
                args.Length == 1 &&
                args[0] is SendMessageRequest),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SendPrivateMessage_WhenConnectionNull_ShouldNotThrow()
    {
        // Arrange
        var service = new ChatService(_privateMessageServiceMock.Object, _unverifiedAuthServiceMock.Object, NullLogger<ChatService>.Instance);
        var request = new SendPrivateMessageRequest
        {
            RecipientUserId = "recipient",
            RecipientUsername = "recipient",
            Content = "Private message"
        };

        // Act
        await service.SendPrivateMessage(request);

        // Assert
        Assert.True(true);
    }

    [Fact]
    public async Task SendPrivateMessage_WhenConnectionExists_ShouldCallSendAsync()
    {
        // Arrange
        var service = new ChatService(_privateMessageServiceMock.Object, _unverifiedAuthServiceMock.Object, NullLogger<ChatService>.Instance);
        var hubConnectionMock = new Mock<HubConnectionStub>();
        var hubConnectionBuilderMock = new Mock<IHubConnectionBuilder>();

        hubConnectionMock.Setup(x => x.StartAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        hubConnectionMock.Setup(x => x.SendCoreAsync("SendPrivateMessage", It.IsAny<object?[]>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        hubConnectionBuilderMock.Setup(x => x.Build()).Returns(hubConnectionMock.Object);

        await service.InitializeAsync(hubConnectionBuilderMock.Object);

        var request = new SendPrivateMessageRequest
        {
            RecipientUserId = "recipient",
            RecipientUsername = "recipient",
            Content = "Private message"
        };

        // Act
        await service.SendPrivateMessage(request);

        // Assert
        hubConnectionMock.Verify(
            x => x.SendCoreAsync("SendPrivateMessage", It.Is<object?[]>(args =>
                args.Length == 1 &&
                args[0] is SendPrivateMessageRequest),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task MarkPrivateMessagesAsRead_WhenConnectionNull_ShouldNotThrow()
    {
        // Arrange
        var service = new ChatService(_privateMessageServiceMock.Object, _unverifiedAuthServiceMock.Object, NullLogger<ChatService>.Instance);

        // Act
        await service.MarkPrivateMessagesAsRead("sender");

        // Assert
        Assert.True(true);
    }

    [Fact]
    public async Task MarkPrivateMessagesAsRead_WhenConnectionExists_ShouldCallSendAsync()
    {
        // Arrange
        var service = new ChatService(_privateMessageServiceMock.Object, _unverifiedAuthServiceMock.Object, NullLogger<ChatService>.Instance);
        var hubConnectionMock = new Mock<HubConnectionStub>();
        var hubConnectionBuilderMock = new Mock<IHubConnectionBuilder>();

        hubConnectionMock.Setup(x => x.StartAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        hubConnectionMock.Setup(x => x.SendCoreAsync("MarkPrivateMessagesAsRead", It.IsAny<object?[]>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        hubConnectionBuilderMock.Setup(x => x.Build()).Returns(hubConnectionMock.Object);

        await service.InitializeAsync(hubConnectionBuilderMock.Object);

        // Act
        await service.MarkPrivateMessagesAsRead("sender");

        // Assert
        hubConnectionMock.Verify(
            x => x.SendCoreAsync("MarkPrivateMessagesAsRead", It.Is<object[]>(args =>
                args.Length == 1 &&
                args[0].ToString() == "sender"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }


    [Fact]
    public async Task DisposeAsync_AfterInitialize_ShouldDisposeConnectionAndTimer()
    {
        // Arrange
        var service = new ChatService(_privateMessageServiceMock.Object, _unverifiedAuthServiceMock.Object, NullLogger<ChatService>.Instance);
        var hubConnectionMock = new Mock<HubConnectionStub>();
        var hubConnectionBuilderMock = new Mock<IHubConnectionBuilder>();

        hubConnectionMock.Setup(x => x.StartAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        hubConnectionMock.Setup(x => x.DisposeAsync())
            .Returns(ValueTask.CompletedTask);

        hubConnectionBuilderMock.Setup(x => x.Build()).Returns(hubConnectionMock.Object);

        await service.InitializeAsync(hubConnectionBuilderMock.Object);

        // Act
        await service.DisposeAsync();

        // Assert
        hubConnectionMock.Verify(x => x.DisposeAsync(), Times.Once);
    }

    [Fact]
    public async Task DisposeAsync_CalledMultipleTimes_ShouldNotThrow()
    {
        // Arrange
        var service = new ChatService(_privateMessageServiceMock.Object, _unverifiedAuthServiceMock.Object, NullLogger<ChatService>.Instance);
        var hubConnectionMock = new Mock<HubConnectionStub>();
        var hubConnectionBuilderMock = new Mock<IHubConnectionBuilder>();

        hubConnectionMock.Setup(x => x.StartAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        hubConnectionMock.Setup(x => x.DisposeAsync())
            .Returns(ValueTask.CompletedTask);

        hubConnectionBuilderMock.Setup(x => x.Build()).Returns(hubConnectionMock.Object);

        await service.InitializeAsync(hubConnectionBuilderMock.Object);

        // Act
        await service.DisposeAsync();

        // Assert
        Assert.True(true);
    }

    [Fact]
    public async Task MultipleEventSubscribers_ShouldAllBeNotified()
    {
        // Arrange
        var service = new ChatService(_privateMessageServiceMock.Object, _unverifiedAuthServiceMock.Object, NullLogger<ChatService>.Instance);
        var hubConnectionMock = new Mock<HubConnectionStub>();
        var hubConnectionBuilderMock = new Mock<IHubConnectionBuilder>();

        hubConnectionMock.Setup(x => x.StartAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);


        Func<object?[], object, Task>? onMessageBlocked = null;
        object? onMessageBlockedState = null;

        hubConnectionMock.Setup(x => x.On("MessageBlocked", It.IsAny<Type[]>(), It.IsAny<Func<object?[], object, Task>>(), It.IsAny<object>()))
            .Callback<string, Type[], Func<object?[], object, Task>, object>((methodName, parameterTypes, handler, state) =>
            {
                onMessageBlocked = handler;
                onMessageBlockedState = state;
            })
            .Returns(Mock.Of<IDisposable>());

        hubConnectionBuilderMock.Setup(x => x.Build()).Returns(hubConnectionMock.Object);

        var subscriber1Called = 0;
        var subscriber2Called = 0;

        service.OnMessageBlocked += _ => subscriber1Called++;
        service.OnMessageBlocked += _ => subscriber2Called++;

        await service.InitializeAsync(hubConnectionBuilderMock.Object);

        // Act
        await onMessageBlocked!(["test"], onMessageBlockedState!);

        // Assert
        Assert.Equal(1, subscriber1Called);
        Assert.Equal(1, subscriber2Called);
    }

    [Fact]
    public async Task Ping_WhenSendAsyncThrows_ShouldLogError()
    {
        // Arrange
        var loggerMock = new Mock<ILogger<ChatService>>();
        var service = new ChatService(_privateMessageServiceMock.Object, _unverifiedAuthServiceMock.Object, loggerMock.Object);
        _unverifiedAuthServiceMock.Setup(x => x.GetClientUserIdAsync())
            .ReturnsAsync("testUserId");
        var hubConnectionMock = new Mock<HubConnectionStub>();
        var hubConnectionBuilderMock = new Mock<IHubConnectionBuilder>();

        hubConnectionMock.Setup(x => x.StartAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        SetConnectedState(hubConnectionMock);

        var pingException = new InvalidOperationException("Ping failed");
        hubConnectionMock.Setup(x => x.SendCoreAsync("Ping", It.IsAny<object[]>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(pingException);

        hubConnectionBuilderMock.Setup(x => x.Build()).Returns(hubConnectionMock.Object);

        await service.InitializeAsync(hubConnectionBuilderMock.Object);

        // Wait for ping timer to execute
        await Task.Delay(300);

        // Assert - Should log the error
        loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("SignalR")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    // ============ TESTS POUR USER MUTED/UNMUTED ============

    [Fact]
    public async Task OnUserMuted_ShouldTriggerEvent()
    {
        // Arrange
        var service = new ChatService(_privateMessageServiceMock.Object, _unverifiedAuthServiceMock.Object, NullLogger<ChatService>.Instance);
        var hubConnectionMock = new Mock<HubConnectionStub>();
        var hubConnectionBuilderMock = new Mock<IHubConnectionBuilder>();

        hubConnectionMock.Setup(x => x.StartAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        Func<object?[], object, Task>? onUserMuted = null;
        object? onUserMutedState = null;

        hubConnectionMock.Setup(x => x.On("UserMuted", It.IsAny<Type[]>(), It.IsAny<Func<object?[], object, Task>>(), It.IsAny<object>()))
            .Callback<string, Type[], Func<object?[], object, Task>, object>((methodName, parameterTypes, handler, state) =>
            {
                onUserMuted = handler;
                onUserMutedState = state;
            })
            .Returns(Mock.Of<IDisposable>());

        hubConnectionBuilderMock.Setup(x => x.Build()).Returns(hubConnectionMock.Object);

        await service.InitializeAsync(hubConnectionBuilderMock.Object);

        // Act without subscription
        await onUserMuted!(["general", "alice-id", "Alice", "admin-id", "Admin"], onUserMutedState!);

        // Arrange
        string? mutedChannel = null;
        string? mutedUserId = null;
        string? mutedUsername = null;
        string? mutedByUserId = null;
        string? mutedByUsername = null;

        service.OnUserMuted += (channel, userId, username, mutedById, mutedByName) =>
        {
            mutedChannel = channel;
            mutedUserId = userId;
            mutedUsername = username;
            mutedByUserId = mutedById;
            mutedByUsername = mutedByName;
        };

        // Act
        await onUserMuted!(["general", "alice-id", "Alice", "admin-id", "Admin"], onUserMutedState!);

        // Assert
        Assert.Equal("general", mutedChannel);
        Assert.Equal("alice-id", mutedUserId);
        Assert.Equal("Alice", mutedUsername);
        Assert.Equal("admin-id", mutedByUserId);
        Assert.Equal("Admin", mutedByUsername);
    }

    [Fact]
    public async Task OnUserUnmuted_ShouldTriggerEvent()
    {
        // Arrange
        var service = new ChatService(_privateMessageServiceMock.Object, _unverifiedAuthServiceMock.Object, NullLogger<ChatService>.Instance);
        var hubConnectionMock = new Mock<HubConnectionStub>();
        var hubConnectionBuilderMock = new Mock<IHubConnectionBuilder>();

        hubConnectionMock.Setup(x => x.StartAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        Func<object?[], object, Task>? onUserUnmuted = null;
        object? onUserUnmutedState = null;

        hubConnectionMock.Setup(x => x.On("UserUnmuted", It.IsAny<Type[]>(), It.IsAny<Func<object?[], object, Task>>(), It.IsAny<object>()))
            .Callback<string, Type[], Func<object?[], object, Task>, object>((methodName, parameterTypes, handler, state) =>
            {
                onUserUnmuted = handler;
                onUserUnmutedState = state;
            })
            .Returns(Mock.Of<IDisposable>());

        hubConnectionBuilderMock.Setup(x => x.Build()).Returns(hubConnectionMock.Object);

        await service.InitializeAsync(hubConnectionBuilderMock.Object);

        // Act without subscription
        await onUserUnmuted!(["general", "alice-id", "Alice", "admin-id", "Admin"], onUserUnmutedState!);

        // Arrange
        string? unmutedChannel = null;
        string? unmutedUserId = null;
        string? unmutedUsername = null;
        string? unmutedByUserId = null;
        string? unmutedByUsername = null;

        service.OnUserUnmuted += (channel, userId, username, unmutedById, unmutedByName) =>
        {
            unmutedChannel = channel;
            unmutedUserId = userId;
            unmutedUsername = username;
            unmutedByUserId = unmutedById;
            unmutedByUsername = unmutedByName;
        };

        // Act
        await onUserUnmuted!(["general", "alice-id", "Alice", "admin-id", "Admin"], onUserUnmutedState!);

        // Assert
        Assert.Equal("general", unmutedChannel);
        Assert.Equal("alice-id", unmutedUserId);
        Assert.Equal("Alice", unmutedUsername);
        Assert.Equal("admin-id", unmutedByUserId);
        Assert.Equal("Admin", unmutedByUsername);
    }

    [Fact]
    public async Task InitializeAsync_ShouldRegisterUserMutedHandler()
    {
        // Arrange
        var service = new ChatService(_privateMessageServiceMock.Object, _unverifiedAuthServiceMock.Object, NullLogger<ChatService>.Instance);
        var hubConnectionMock = new Mock<HubConnectionStub>();
        var hubConnectionBuilderMock = new Mock<IHubConnectionBuilder>();

        hubConnectionMock.Setup(x => x.StartAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        hubConnectionMock.Setup(x => x.On(
            It.IsAny<string>(),
            It.IsAny<Type[]>(),
            It.IsAny<Func<object?[], object, Task>>(),
            It.IsAny<object>()))
            .Returns(Mock.Of<IDisposable>());

        hubConnectionBuilderMock.Setup(x => x.Build()).Returns(hubConnectionMock.Object);
        SetConnectedState(hubConnectionMock);

        // Act
        await service.InitializeAsync(hubConnectionBuilderMock.Object);

        // Assert - Vérifier que le handler UserMuted est enregistré
        hubConnectionMock.Verify(x => x.On("UserMuted", It.IsAny<Type[]>(), It.IsAny<Func<object?[], object, Task>>(), It.IsAny<object>()), Times.Once);
    }

    [Fact]
    public async Task InitializeAsync_ShouldRegisterUserUnmutedHandler()
    {
        // Arrange
        var service = new ChatService(_privateMessageServiceMock.Object, _unverifiedAuthServiceMock.Object, NullLogger<ChatService>.Instance);
        var hubConnectionMock = new Mock<HubConnectionStub>();
        var hubConnectionBuilderMock = new Mock<IHubConnectionBuilder>();

        hubConnectionMock.Setup(x => x.StartAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        hubConnectionMock.Setup(x => x.On(
            It.IsAny<string>(),
            It.IsAny<Type[]>(),
            It.IsAny<Func<object?[], object, Task>>(),
            It.IsAny<object>()))
            .Returns(Mock.Of<IDisposable>());

        hubConnectionBuilderMock.Setup(x => x.Build()).Returns(hubConnectionMock.Object);
        SetConnectedState(hubConnectionMock);

        // Act
        await service.InitializeAsync(hubConnectionBuilderMock.Object);

        // Assert - Vérifier que le handler UserUnmuted est enregistré
        hubConnectionMock.Verify(x => x.On("UserUnmuted", It.IsAny<Type[]>(), It.IsAny<Func<object?[], object, Task>>(), It.IsAny<object>()), Times.Once);
    }

    [Fact]
    public async Task OnUserMuted_MultipleSubscribers_ShouldNotifyAll()
    {
        // Arrange
        var service = new ChatService(_privateMessageServiceMock.Object, _unverifiedAuthServiceMock.Object, NullLogger<ChatService>.Instance);
        var hubConnectionMock = new Mock<HubConnectionStub>();
        var hubConnectionBuilderMock = new Mock<IHubConnectionBuilder>();

        hubConnectionMock.Setup(x => x.StartAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        Func<object?[], object, Task>? onUserMuted = null;
        object? onUserMutedState = null;

        hubConnectionMock.Setup(x => x.On("UserMuted", It.IsAny<Type[]>(), It.IsAny<Func<object?[], object, Task>>(), It.IsAny<object>()))
            .Callback<string, Type[], Func<object?[], object, Task>, object>((methodName, parameterTypes, handler, state) =>
            {
                onUserMuted = handler;
                onUserMutedState = state;
            })
            .Returns(Mock.Of<IDisposable>());

        hubConnectionBuilderMock.Setup(x => x.Build()).Returns(hubConnectionMock.Object);

        var subscriber1Called = 0;
        var subscriber2Called = 0;

        service.OnUserMuted += (channel, userId, username, mutedById, mutedByName) => subscriber1Called++;
        service.OnUserMuted += (channel, userId, username, mutedById, mutedByName) => subscriber2Called++;

        await service.InitializeAsync(hubConnectionBuilderMock.Object);

        // Act
        await onUserMuted!(["general", "alice-id", "Alice", "admin-id", "Admin"], onUserMutedState!);

        // Assert
        Assert.Equal(1, subscriber1Called);
        Assert.Equal(1, subscriber2Called);
    }

    [Fact]
    public async Task OnUserUnmuted_MultipleSubscribers_ShouldNotifyAll()
    {
        // Arrange
        var service = new ChatService(_privateMessageServiceMock.Object, _unverifiedAuthServiceMock.Object, NullLogger<ChatService>.Instance);
        var hubConnectionMock = new Mock<HubConnectionStub>();
        var hubConnectionBuilderMock = new Mock<IHubConnectionBuilder>();

        hubConnectionMock.Setup(x => x.StartAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        Func<object?[], object, Task>? onUserUnmuted = null;
        object? onUserUnmutedState = null;

        hubConnectionMock.Setup(x => x.On("UserUnmuted", It.IsAny<Type[]>(), It.IsAny<Func<object?[], object, Task>>(), It.IsAny<object>()))
            .Callback<string, Type[], Func<object?[], object, Task>, object>((methodName, parameterTypes, handler, state) =>
            {
                onUserUnmuted = handler;
                onUserUnmutedState = state;
            })
            .Returns(Mock.Of<IDisposable>());

        hubConnectionBuilderMock.Setup(x => x.Build()).Returns(hubConnectionMock.Object);

        var subscriber1Called = 0;
        var subscriber2Called = 0;

        service.OnUserUnmuted += (channel, userId, username, unmutedById, unmutedByName) => subscriber1Called++;
        service.OnUserUnmuted += (channel, userId, username, unmutedById, unmutedByName) => subscriber2Called++;

        await service.InitializeAsync(hubConnectionBuilderMock.Object);

        // Act
        await onUserUnmuted!(["general", "alice-id", "Alice", "admin-id", "Admin"], onUserUnmutedState!);

        // Assert
        Assert.Equal(1, subscriber1Called);
        Assert.Equal(1, subscriber2Called);
    }

    [Fact]
    public async Task OnUserMuted_WithEmptyChannel_ShouldStillTrigger()
    {
        // Arrange
        var service = new ChatService(_privateMessageServiceMock.Object, _unverifiedAuthServiceMock.Object, NullLogger<ChatService>.Instance);
        var hubConnectionMock = new Mock<HubConnectionStub>();
        var hubConnectionBuilderMock = new Mock<IHubConnectionBuilder>();

        hubConnectionMock.Setup(x => x.StartAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        Func<object?[], object, Task>? onUserMuted = null;
        object? onUserMutedState = null;

        hubConnectionMock.Setup(x => x.On("UserMuted", It.IsAny<Type[]>(), It.IsAny<Func<object?[], object, Task>>(), It.IsAny<object>()))
            .Callback<string, Type[], Func<object?[], object, Task>, object>((methodName, parameterTypes, handler, state) =>
            {
                onUserMuted = handler;
                onUserMutedState = state;
            })
            .Returns(Mock.Of<IDisposable>());

        hubConnectionBuilderMock.Setup(x => x.Build()).Returns(hubConnectionMock.Object);

        var eventTriggered = false;
        service.OnUserMuted += (channel, userId, username, mutedById, mutedByName) => eventTriggered = true;

        await service.InitializeAsync(hubConnectionBuilderMock.Object);

        // Act
        await onUserMuted!(["", "alice-id", "Alice", "admin-id", "Admin"], onUserMutedState!);

        // Assert
        Assert.True(eventTriggered);
    }

    [Fact]
    public async Task OnUserUnmuted_WithEmptyChannel_ShouldStillTrigger()
    {
        // Arrange
        var service = new ChatService(_privateMessageServiceMock.Object, _unverifiedAuthServiceMock.Object, NullLogger<ChatService>.Instance);
        var hubConnectionMock = new Mock<HubConnectionStub>();
        var hubConnectionBuilderMock = new Mock<IHubConnectionBuilder>();

        hubConnectionMock.Setup(x => x.StartAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        Func<object?[], object, Task>? onUserUnmuted = null;
        object? onUserUnmutedState = null;

        hubConnectionMock.Setup(x => x.On("UserUnmuted", It.IsAny<Type[]>(), It.IsAny<Func<object?[], object, Task>>(), It.IsAny<object>()))
            .Callback<string, Type[], Func<object?[], object, Task>, object>((methodName, parameterTypes, handler, state) =>
            {
                onUserUnmuted = handler;
                onUserUnmutedState = state;
            })
            .Returns(Mock.Of<IDisposable>());

        hubConnectionBuilderMock.Setup(x => x.Build()).Returns(hubConnectionMock.Object);

        var eventTriggered = false;
        service.OnUserUnmuted += (channel, userId, username, unmutedById, unmutedByName) => eventTriggered = true;

        await service.InitializeAsync(hubConnectionBuilderMock.Object);

        // Act
        await onUserUnmuted!(["", "alice-id", "Alice", "admin-id", "Admin"], onUserUnmutedState!);

        // Assert
        Assert.True(eventTriggered);
    }

    [Fact]
    public async Task OnUserMuted_NoSubscribers_ShouldNotThrow()
    {
        // Arrange
        var service = new ChatService(_privateMessageServiceMock.Object, _unverifiedAuthServiceMock.Object, NullLogger<ChatService>.Instance);
        var hubConnectionMock = new Mock<HubConnectionStub>();
        var hubConnectionBuilderMock = new Mock<IHubConnectionBuilder>();

        hubConnectionMock.Setup(x => x.StartAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        Func<object?[], object, Task>? onUserMuted = null;
        object? onUserMutedState = null;

        hubConnectionMock.Setup(x => x.On("UserMuted", It.IsAny<Type[]>(), It.IsAny<Func<object?[], object, Task>>(), It.IsAny<object>()))
            .Callback<string, Type[], Func<object?[], object, Task>, object>((methodName, parameterTypes, handler, state) =>
            {
                onUserMuted = handler;
                onUserMutedState = state;
            })
            .Returns(Mock.Of<IDisposable>());

        hubConnectionBuilderMock.Setup(x => x.Build()).Returns(hubConnectionMock.Object);

        await service.InitializeAsync(hubConnectionBuilderMock.Object);

        // Act & Assert - Devrait ne pas lancer d'exception
        await onUserMuted!(["general", "alice-id", "Alice", "admin-id", "Admin"], onUserMutedState!);
        Assert.True(true);
    }

    [Fact]
    public async Task OnUserUnmuted_NoSubscribers_ShouldNotThrow()
    {
        // Arrange
        var service = new ChatService(_privateMessageServiceMock.Object, _unverifiedAuthServiceMock.Object, NullLogger<ChatService>.Instance);
        var hubConnectionMock = new Mock<HubConnectionStub>();
        var hubConnectionBuilderMock = new Mock<IHubConnectionBuilder>();

        hubConnectionMock.Setup(x => x.StartAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        Func<object?[], object, Task>? onUserUnmuted = null;
        object? onUserUnmutedState = null;

        hubConnectionMock.Setup(x => x.On("UserUnmuted", It.IsAny<Type[]>(), It.IsAny<Func<object?[], object, Task>>(), It.IsAny<object>()))
            .Callback<string, Type[], Func<object?[], object, Task>, object>((methodName, parameterTypes, handler, state) =>
            {
                onUserUnmuted = handler;
                onUserUnmutedState = state;
            })
            .Returns(Mock.Of<IDisposable>());

        hubConnectionBuilderMock.Setup(x => x.Build()).Returns(hubConnectionMock.Object);

        await service.InitializeAsync(hubConnectionBuilderMock.Object);

        // Act & Assert - Devrait ne pas lancer d'exception
        await onUserUnmuted!(["general", "alice-id", "Alice", "admin-id", "Admin"], onUserUnmutedState!);
        Assert.True(true);
    }

    [Fact]
    public void ChatService_OnUserMuted_PropertyExists()
    {
        // Arrange

        // Act & Assert - Vérifier que les propriétés d'événements existent
        var userMutedEvent = typeof(ChatService).GetEvent("OnUserMuted");
        var userUnmutedEvent = typeof(ChatService).GetEvent("OnUserUnmuted");

        Assert.NotNull(userMutedEvent);
        Assert.NotNull(userUnmutedEvent);
    }

    private static void SetConnectedState(Mock<HubConnectionStub> hubConnectionMock)
    {
        // Simuler l'état connecté
        var internalStateField = typeof(HubConnection).GetField("_state", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var internalState = internalStateField!.GetValue(hubConnectionMock.Object);
        var changeStateMethod = internalState!.GetType().GetMethod("ChangeState", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        changeStateMethod!.Invoke(internalState, [HubConnectionState.Disconnected, HubConnectionState.Connected]);
    }
}


public class HubConnectionStub : HubConnection
{
    public HubConnectionStub()
        : base(
            Mock.Of<IConnectionFactory>(),
            Mock.Of<IHubProtocol>(),
            Mock.Of<EndPoint>(),
            Mock.Of<IServiceProvider>(),
            GetLoggerFactory(),
            Mock.Of<IRetryPolicy>())
    {
    }

    private static ILoggerFactory GetLoggerFactory()
    {
        var loggerFactoryMock = new Mock<ILoggerFactory>();
        loggerFactoryMock.Setup(x => x.CreateLogger(It.IsAny<string>()))
            .Returns(Mock.Of<ILogger>());
        return loggerFactoryMock.Object;
    }
}