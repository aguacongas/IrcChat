// tests/IrcChat.Client.Tests/Pages/ChatTests.cs
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Http.Json;
using Bunit;
using IrcChat.Client.Models;
using IrcChat.Client.Pages;
using IrcChat.Client.Services;
using IrcChat.Shared.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using RichardSzalay.MockHttp;
using Xunit;

namespace IrcChat.Client.Tests.Pages;

public partial class ChatTests : BunitContext
{
    private readonly Mock<IChatService> _chatServiceMock;
    private readonly Mock<IUnifiedAuthService> _authServiceMock;
    private readonly Mock<IPrivateMessageService> _privateMessageServiceMock;
    private readonly Mock<IDeviceDetectorService> _deviceDetectorMock;
    private readonly Mock<IIgnoredUsersService> _ignoredUsersServiceMock;
    private readonly Mock<IActiveChannelsService> _activeChannelsServiceMock;
    private readonly MockHttpMessageHandler _mockHttp;
    private readonly NavigationManager _navManager;

    public ChatTests()
    {
        _chatServiceMock = new Mock<IChatService>();
        _authServiceMock = new Mock<IUnifiedAuthService>();
        _privateMessageServiceMock = new Mock<IPrivateMessageService>();
        _mockHttp = new MockHttpMessageHandler();
        _deviceDetectorMock = new Mock<IDeviceDetectorService>();
        _ignoredUsersServiceMock = new Mock<IIgnoredUsersService>();
        _activeChannelsServiceMock = new Mock<IActiveChannelsService>();

        _deviceDetectorMock.Setup(x => x.IsMobileDeviceAsync()).ReturnsAsync(false);
        _ignoredUsersServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);
        _ignoredUsersServiceMock.Setup(x => x.IsUserIgnored(It.IsAny<string>())).Returns(false);
        _activeChannelsServiceMock.Setup(x => x.GetActiveChannelsAsync()).ReturnsAsync([]);

        var httpClient = _mockHttp.ToHttpClient();
        httpClient.BaseAddress = new Uri("https://localhost:7000");

        Services.AddSingleton(_chatServiceMock.Object);
        Services.AddSingleton(_authServiceMock.Object);
        Services.AddSingleton(_privateMessageServiceMock.Object);
        Services.AddSingleton(_deviceDetectorMock.Object);
        Services.AddSingleton(_ignoredUsersServiceMock.Object);
        Services.AddSingleton(_activeChannelsServiceMock.Object);

        Services.AddSingleton(httpClient);
        Services.Configure<ApiSettings>(s =>
        {
            s.BaseUrl = "https://localhost:7000";
            s.SignalRHubUrl = "https://localhost:7000/chathub";
        });
        _navManager = Services.GetRequiredService<NavigationManager>();
    }



    [Fact]
    public void Chat_WhenNoUsername_ShouldRedirectToLogin()
    {
        _authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);
        _authServiceMock.Setup(x => x.HasUsername).Returns(false);
        Render<Chat>();
        Assert.EndsWith("/login", _navManager.Uri);
    }

    [Fact]
    public async Task Chat_OnInitialization_ShouldInitializeIgnoredUsersService()
    {
        SetupBasicAuth();
        SetupBasicMocks();
        Render<Chat>();
        await Task.Delay(200);
        _ignoredUsersServiceMock.Verify(x => x.InitializeAsync(), Times.Once);
    }

    [Fact]
    public async Task Chat_OnInitialization_ShouldSubscribeToIgnoredUsersChangedEvent()
    {
        SetupBasicAuth();
        SetupBasicMocks();
        Render<Chat>();
        await Task.Delay(200);
        _ignoredUsersServiceMock.VerifyAdd(x => x.OnIgnoredUsersChanged += It.IsAny<Action>());
    }

    [Fact]
    [SuppressMessage("Major Code Smell", "S6966:Awaitable method should be used", Justification = "Raise doesn't support async")]
    public async Task Chat_OnMessageReceived_WhenUserIsIgnored_ShouldNotAddMessage()
    {
        SetupBasicAuth();
        var channels = new List<Channel>
        {
            new() { Id = Guid.NewGuid(), Name = "general", CreatedBy = "system", CreatedAt = DateTime.UtcNow }
        };
        _mockHttp.When(HttpMethod.Get, "*/api/channels")
            .Respond(HttpStatusCode.OK, request => JsonContent.Create(channels));
        _mockHttp.When(HttpMethod.Get, "*/api/messages/general")
            .Respond(HttpStatusCode.OK, request => JsonContent.Create(new List<Message>()));
        _mockHttp.When(HttpMethod.Get, "*/api/channels/general/users")
            .Respond(HttpStatusCode.OK, request => JsonContent.Create(new List<User>()));
        _chatServiceMock.Setup(x => x.InitializeAsync(It.IsAny<IHubConnectionBuilder>()))
            .Returns(Task.CompletedTask);
        _chatServiceMock.Setup(x => x.JoinChannel("general")).Returns(Task.CompletedTask);
        _privateMessageServiceMock.Setup(x => x.GetConversationsAsync(It.IsAny<string>()))
            .ReturnsAsync([]);
        _ignoredUsersServiceMock.Setup(x => x.IsUserIgnored("IgnoredUser")).Returns(true);

        var cut = await RenderChatAsync(channelName: "general");

        var ignoredMessage = new Message
        {
            Id = Guid.NewGuid(),
            Username = "IgnoredUser",
            UserId = "IgnoredUser",
            Content = "Message from ignored user",
            Channel = "general",
            Timestamp = DateTime.UtcNow
        };
        _chatServiceMock.Raise(x => x.OnMessageReceived += null, ignoredMessage);
        await Task.Delay(100);
        cut.Render();

        Assert.DoesNotContain("Message from ignored user", cut.Markup);
    }

    [Fact]
    [SuppressMessage("Major Code Smell", "S6966:Awaitable method should be used", Justification = "Raise doesn't support async")]
    public async Task Chat_OnMessageReceived_WhenUserIsNotIgnored_ShouldAddMessage()
    {
        SetupBasicAuth();
        var channels = new List<Channel>
        {
            new() { Id = Guid.NewGuid(), Name = "general", CreatedBy = "system", CreatedAt = DateTime.UtcNow }
        };
        _mockHttp.When(HttpMethod.Get, "*/api/channels")
            .Respond(HttpStatusCode.OK, request => JsonContent.Create(channels));
        _mockHttp.When(HttpMethod.Get, "*/api/my-channels?username=TestUser")
            .Respond(HttpStatusCode.OK, request => JsonContent.Create(channels));
        _mockHttp.When(HttpMethod.Get, "*/api/messages/general")
            .Respond(HttpStatusCode.OK, request => JsonContent.Create(new List<Message>()));
        _mockHttp.When(HttpMethod.Get, "*/api/channels/general/users")
            .Respond(HttpStatusCode.OK, request => JsonContent.Create(new List<User>()));
        _chatServiceMock.Setup(x => x.InitializeAsync(It.IsAny<IHubConnectionBuilder>()))
            .Returns(Task.CompletedTask);
        _chatServiceMock.Setup(x => x.JoinChannel("general")).Returns(Task.CompletedTask);
        _privateMessageServiceMock.Setup(x => x.GetConversationsAsync(It.IsAny<string>()))
            .ReturnsAsync([]);
        _ignoredUsersServiceMock.Setup(x => x.IsUserIgnored("NormalUser")).Returns(false);

        var cut = await RenderChatAsync(channelName: "general");

        var normalMessage = new Message
        {
            Id = Guid.NewGuid(),
            Username = "NormalUser",
            UserId = "NormalUser",
            Content = "Message from normal user",
            Channel = "general",
            Timestamp = DateTime.UtcNow
        };
        _chatServiceMock.Raise(x => x.OnMessageReceived += null, normalMessage);
        await Task.Delay(100);
        cut.Render();

        Assert.Contains("Message from normal user", cut.Markup);
    }

    [Fact]
    public async Task Chat_LoadMessages_ShouldFilterIgnoredUsers()
    {
        SetupBasicAuth();
        var channels = new List<Channel>
        {
            new() { Id = Guid.NewGuid(), Name = "general", CreatedBy = "system", CreatedAt = DateTime.UtcNow }
        };
        var messages = new List<Message>
        {
            new() { Id = Guid.NewGuid(), Username = "NormalUser", UserId = "NormalUser", Content = "Normal message", Channel = "general", Timestamp = DateTime.UtcNow },
            new() { Id = Guid.NewGuid(), Username = "IgnoredUser", UserId = "IgnoredUser", Content = "Ignored message", Channel = "general", Timestamp = DateTime.UtcNow }
        };
        _mockHttp.When(HttpMethod.Get, "*/api/channels")
            .Respond(HttpStatusCode.OK, request => JsonContent.Create(channels));
        _mockHttp.When(HttpMethod.Get, "*/api/my-channels?username=TestUser")
            .Respond(HttpStatusCode.OK, request => JsonContent.Create(channels));
        _mockHttp.When(HttpMethod.Get, "*/api/messages/general")
            .Respond(HttpStatusCode.OK, request => JsonContent.Create(messages));
        _mockHttp.When(HttpMethod.Get, "*/api/channels/general/users")
            .Respond(HttpStatusCode.OK, request => JsonContent.Create(new List<User>()));
        _chatServiceMock.Setup(x => x.InitializeAsync(It.IsAny<IHubConnectionBuilder>()))
            .Returns(Task.CompletedTask);
        _chatServiceMock.Setup(x => x.JoinChannel("general")).Returns(Task.CompletedTask);
        _privateMessageServiceMock.Setup(x => x.GetConversationsAsync(It.IsAny<string>()))
            .ReturnsAsync([]);
        _ignoredUsersServiceMock.Setup(x => x.IsUserIgnored("IgnoredUser")).Returns(true);
        _ignoredUsersServiceMock.Setup(x => x.IsUserIgnored("NormalUser")).Returns(false);

        var cut = await RenderChatAsync(channelName: "general");
        cut.Render();

        Assert.Contains("Normal message", cut.Markup);
        Assert.DoesNotContain("Ignored message", cut.Markup);
    }

    [Fact]
    public async Task Chat_LoadPrivateConversations_ShouldFilterIgnoredUsers()
    {
        SetupBasicAuth();
        SetupBasicMocks();
        var conversations = new List<PrivateConversation>
        {
            new() { OtherUser = new User { UserId = "NormalUser", Username = "NormalUser" }, LastMessage = "Hi", LastMessageTime = DateTime.UtcNow, UnreadCount = 1 },
            new() { OtherUser = new User { UserId = "IgnoredUser", Username = "IgnoredUser" }, LastMessage = "Hello", LastMessageTime = DateTime.UtcNow, UnreadCount = 2 }
        };
        _privateMessageServiceMock.Setup(x => x.GetConversationsAsync("TestUser"))
            .ReturnsAsync(conversations);
        _ignoredUsersServiceMock.Setup(x => x.IsUserIgnored("IgnoredUser")).Returns(true);
        _ignoredUsersServiceMock.Setup(x => x.IsUserIgnored("NormalUser")).Returns(false);

        var cut = Render<Chat>();
        await Task.Delay(200);

        Assert.Contains("NormalUser", cut.Markup);
        Assert.DoesNotContain("IgnoredUser", cut.Markup);
    }

    [Fact]
    [SuppressMessage("Major Code Smell", "S6966:Awaitable method should be used", Justification = "Raise doesn't support async")]
    public async Task Chat_OnPrivateMessageReceived_WhenSenderIsIgnored_ShouldNotAddMessage()
    {
        SetupBasicAuth();
        _mockHttp.When(HttpMethod.Get, "*/api/channels")
            .Respond(HttpStatusCode.OK, request => JsonContent.Create(new List<Channel>()));
        _mockHttp.When(HttpMethod.Get, "*/api/private-messages/status/Friend")
            .Respond(HttpStatusCode.OK, request => JsonContent.Create(new { Username = "Friend", IsOnline = true }));
        _chatServiceMock.Setup(x => x.InitializeAsync(It.IsAny<IHubConnectionBuilder>()))
            .Returns(Task.CompletedTask);
        _chatServiceMock.Setup(x => x.MarkPrivateMessagesAsRead(It.IsAny<string>()))
            .Returns(Task.CompletedTask);
        _privateMessageServiceMock.Setup(x => x.GetConversationsAsync("TestUser"))
            .ReturnsAsync([new() { OtherUser = new User { UserId = "Friend", Username = "Friend" }, LastMessage = "Hi", LastMessageTime = DateTime.UtcNow, UnreadCount = 0 }]);
        _privateMessageServiceMock.Setup(x => x.GetPrivateMessagesAsync("TestUser", "Friend"))
            .ReturnsAsync([]);
        _ignoredUsersServiceMock.Setup(x => x.IsUserIgnored("Friend")).Returns(false);
        _ignoredUsersServiceMock.Setup(x => x.IsUserIgnored("IgnoredSender")).Returns(true);

        var cut = await RenderChatAsync();
        await NavigateToPrivateChatAsync(cut, "Friend", "Friend");

        var ignoredMsg = new PrivateMessage
        {
            Id = Guid.NewGuid(),
            SenderUserId = "IgnoredSender",
            SenderUsername = "IgnoredSender",
            RecipientUserId = "TestUser",
            RecipientUsername = "TestUser",
            Content = "Ignored private message",
            Timestamp = DateTime.UtcNow
        };
        _privateMessageServiceMock.Raise(x => x.OnPrivateMessageReceived += null, ignoredMsg);
        await Task.Delay(100);
        cut.Render();

        Assert.DoesNotContain("Ignored private message", cut.Markup);
    }

    [Fact]
    [SuppressMessage("Major Code Smell", "S6966:Awaitable method should be used", Justification = "Raise doesn't support async")]
    public async Task Chat_OnIgnoredUsersChanged_ShouldReloadPrivateChat()
    {
        SetupBasicAuth();
        _mockHttp.When(HttpMethod.Get, "*/api/channels")
            .Respond(HttpStatusCode.OK, request => JsonContent.Create(new List<Channel>()));
        _mockHttp.When(HttpMethod.Get, "*/api/private-messages/status/Friend")
            .Respond(HttpStatusCode.OK, request => JsonContent.Create(new { Username = "Friend", IsOnline = true }));
        _chatServiceMock.Setup(x => x.InitializeAsync(It.IsAny<IHubConnectionBuilder>()))
            .Returns(Task.CompletedTask);
        _chatServiceMock.Setup(x => x.MarkPrivateMessagesAsRead(It.IsAny<string>()))
            .Returns(Task.CompletedTask);
        var conversations = new List<PrivateConversation>
        {
            new() { OtherUser = new User { UserId = "Friend", Username = "Friend" }, LastMessage = "Hi", LastMessageTime = DateTime.UtcNow, UnreadCount = 0 }
        };
        _privateMessageServiceMock.Setup(x => x.GetConversationsAsync("TestUser"))
            .ReturnsAsync(conversations);
        _privateMessageServiceMock.Setup(x => x.GetPrivateMessagesAsync("TestUser", "Friend"))
            .ReturnsAsync([]);
        _ignoredUsersServiceMock.Setup(x => x.IsUserIgnored("Friend")).Returns(false);

        var cut = await RenderChatAsync();
        await NavigateToPrivateChatAsync(cut, "Friend", "Friend");

        _privateMessageServiceMock.Invocations.Clear();
        _ignoredUsersServiceMock.Raise(x => x.OnIgnoredUsersChanged += null);
        await Task.Delay(200);

        _privateMessageServiceMock.Verify(x => x.GetPrivateMessagesAsync("TestUser", "Friend"), Times.AtLeastOnce);
    }

    [Fact]
    public async Task Chat_DisposeAsync_ShouldUnsubscribeFromIgnoredUsersChangedEvent()
    {
        SetupBasicAuth();
        SetupBasicMocks();
        var cut = Render<Chat>();
        await Task.Delay(200);
        await cut.Instance.DisposeAsync();
        _ignoredUsersServiceMock.VerifyRemove(x => x.OnIgnoredUsersChanged -= It.IsAny<Action>());
    }

    [Fact]
    public async Task Chat_OnInitialization_ShouldLoadChannels()
    {
        SetupBasicAuth();
        _authServiceMock.Setup(x => x.Token).Returns("test-token");
        var channels = new List<Channel>
        {
            new() { Id = Guid.NewGuid(), Name = "general", CreatedBy = "system", CreatedAt = DateTime.UtcNow },
            new() { Id = Guid.NewGuid(), Name = "random", CreatedBy = "system", CreatedAt = DateTime.UtcNow }
        };
        _mockHttp.When(HttpMethod.Get, "*/api/channels")
            .Respond(HttpStatusCode.OK, request => JsonContent.Create(channels));
        _chatServiceMock.Setup(x => x.InitializeAsync(It.IsAny<IHubConnectionBuilder>()))
            .Returns(Task.CompletedTask);
        _privateMessageServiceMock.Setup(x => x.GetConversationsAsync(It.IsAny<string>()))
            .ReturnsAsync([]);

        var cut = Render<Chat>();
        await Task.Delay(200);

        Assert.Contains("general", cut.Markup);
        Assert.Contains("random", cut.Markup);
    }

    [Fact]
    public async Task Chat_SendMessage_ShouldCallChatService()
    {
        SetupBasicAuth();
        SetupBasicMocks();
        var cut = Render<Chat>();
        await Task.Delay(200);
        Assert.NotNull(cut.Find(".chat-container"));
    }

    [Fact]
    public async Task Chat_JoinChannel_ShouldLoadMessagesAndUsers()
    {
        SetupBasicAuth();
        var channels = new List<Channel>
        {
            new() { Id = Guid.NewGuid(), Name = "general", CreatedBy = "system", CreatedAt = DateTime.UtcNow }
        };
        var messages = new List<Message>
        {
            new() { Id = Guid.NewGuid(), Username = "User1", Content = "Hello", Channel = "general", Timestamp = DateTime.UtcNow }
        };
        _mockHttp.When(HttpMethod.Get, "*/api/channels")
            .Respond(HttpStatusCode.OK, request => JsonContent.Create(channels));
        _mockHttp.When(HttpMethod.Get, "*/api/messages/general")
            .Respond(HttpStatusCode.OK, request => JsonContent.Create(messages));
        _chatServiceMock.Setup(x => x.InitializeAsync(It.IsAny<IHubConnectionBuilder>()))
            .Returns(Task.CompletedTask);
        _chatServiceMock.Setup(x => x.JoinChannel("general")).Returns(Task.CompletedTask);
        _privateMessageServiceMock.Setup(x => x.GetConversationsAsync(It.IsAny<string>()))
            .ReturnsAsync([]);

        var cut = await RenderChatAsync(channelName: "general");
        cut.Render();

        _chatServiceMock.Verify(x => x.JoinChannel(It.IsAny<string>()), Times.AtLeastOnce);
    }

    [Fact]
    [SuppressMessage("Major Code Smell", "S6966:Awaitable method should be used", Justification = "RaiseAsync génère une erreur")]
    public async Task Chat_ReceiveMessage_ShouldUpdateMessageList()
    {
        SetupBasicAuth();
        SetupBasicMocks();
        Render<Chat>();
        await Task.Delay(200);

        var newMessage = new Message
        {
            Id = Guid.NewGuid(),
            Username = "User1",
            Content = "Test message",
            Channel = "general",
            Timestamp = DateTime.UtcNow
        };
        _chatServiceMock.Raise(x => x.OnMessageReceived += null, newMessage);
        await Task.Delay(100);

        _chatServiceMock.VerifyAdd(x => x.OnMessageReceived += It.IsAny<Action<Message>>());
    }

    [Fact]
    public async Task Chat_PrivateMessage_ShouldOpenPrivateChat()
    {
        SetupBasicAuth();
        SetupBasicMocks();
        var conversations = new List<PrivateConversation>
        {
            new() { OtherUser = new User { UserId = "User2", Username = "User2" }, LastMessage = "Hello", LastMessageTime = DateTime.UtcNow, UnreadCount = 1 }
        };
        _privateMessageServiceMock.Setup(x => x.GetConversationsAsync("TestUser"))
            .ReturnsAsync(conversations);
        _privateMessageServiceMock.Setup(x => x.GetPrivateMessagesAsync("TestUser", "User2"))
            .ReturnsAsync([]);

        Render<Chat>();
        await Task.Delay(200);

        _privateMessageServiceMock.Verify(x => x.GetConversationsAsync("TestUser"), Times.AtLeastOnce);
    }

    [Fact]
    [SuppressMessage("Major Code Smell", "S6966:Awaitable method should be used", Justification = "RaiseAsync génère une erreur")]
    public async Task Chat_MuteStatusChange_ShouldUpdateChannel()
    {
        SetupBasicAuth();
        SetupBasicMocks();
        Render<Chat>();
        await Task.Delay(200);

        _chatServiceMock.Raise(x => x.OnChannelMuteStatusChanged += null, "general", true);
        await Task.Delay(100);

        _chatServiceMock.VerifyAdd(x => x.OnChannelMuteStatusChanged += It.IsAny<Action<string, bool>>());
    }

    [Fact]
    [SuppressMessage("Major Code Smell", "S6966:Awaitable method should be used", Justification = "RaiseAsync génère une erreur")]
    public async Task Chat_UserJoined_ShouldUpdateState()
    {
        SetupBasicAuth();
        SetupBasicMocks();
        Render<Chat>();
        await Task.Delay(200);

        _chatServiceMock.Raise(x => x.OnUserJoined += null, "NewUser", "NewUser", "general");
        await Task.Delay(100);

        _chatServiceMock.VerifyAdd(x => x.OnUserJoined += It.IsAny<Action<string, string, string>>());
    }

    [Fact]
    [SuppressMessage("Major Code Smell", "S6966:Awaitable method should be used", Justification = "RaiseAsync génère une erreur")]
    public async Task Chat_UserLeft_ShouldUpdateState()
    {
        SetupBasicAuth();
        SetupBasicMocks();
        Render<Chat>();
        await Task.Delay(200);

        _chatServiceMock.Raise(x => x.OnUserLeft += null, "DepartingUser", "DepartingUser", "general");
        await Task.Delay(100);

        _chatServiceMock.VerifyAdd(x => x.OnUserLeft += It.IsAny<Action<string, string, string>>());
    }

    [Fact]
    [SuppressMessage("Major Code Smell", "S6966:Awaitable method should be used", Justification = "RaiseAsync génère une erreur")]
    public async Task Chat_PrivateMessageReceived_ShouldUpdateConversations()
    {
        SetupBasicAuth();
        SetupBasicMocks();
        Render<Chat>();
        await Task.Delay(200);

        var privateMessage = new PrivateMessage
        {
            Id = Guid.NewGuid(),
            SenderUsername = "Sender",
            RecipientUsername = "TestUser",
            Content = "Private message",
            Timestamp = DateTime.UtcNow
        };
        _privateMessageServiceMock.Raise(x => x.OnPrivateMessageReceived += null, privateMessage);
        await Task.Delay(100);

        _privateMessageServiceMock.VerifyAdd(x => x.OnPrivateMessageReceived += It.IsAny<Action<PrivateMessage>>());
    }

    [Fact]
    [SuppressMessage("Major Code Smell", "S6966:Awaitable method should be used", Justification = "RaiseAsync génère une erreur")]
    public async Task Chat_ConversationDeleted_ShouldClosePrivateChatIfOpen()
    {
        SetupBasicAuth();
        SetupBasicMocks();
        Render<Chat>();
        await Task.Delay(200);

        _privateMessageServiceMock.Raise(x => x.OnConversationDeleted += null, "DeletedUser");
        await Task.Delay(100);

        _privateMessageServiceMock.VerifyAdd(x => x.OnConversationDeleted += It.IsAny<Action<string>>());
    }

    [Fact]
    public async Task Chat_GoToSettings_ShouldNavigateToSettingsPage()
    {
        SetupBasicAuth();
        SetupBasicMocks();
        var cut = Render<Chat>();
        await Task.Delay(200);

        await cut.InvokeAsync(() => cut.Find(".user-info").Click());

        Assert.EndsWith("/settings", _navManager.Uri);
    }

    [Fact]
    [SuppressMessage("Major Code Smell", "S6966:Awaitable method should be used", Justification = "RaiseAsync génère une erreur")]
    public async Task Chat_MessageBlocked_ShouldShowNotification()
    {
        SetupBasicAuth();
        SetupBasicMocks();
        var cut = Render<Chat>();
        await Task.Delay(200);

        _chatServiceMock.Raise(x => x.OnMessageBlocked += null, "Ce salon est muet");
        await Task.Delay(100);
        cut.Render();

        Assert.Contains("mute-notification", cut.Markup);
        Assert.Contains("Ce salon est muet", cut.Markup);
    }

    [Fact]
    public async Task Chat_CanManageCurrentChannel_AdminUser_ShouldReturnTrue()
    {
        _authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);
        _authServiceMock.Setup(x => x.HasUsername).Returns(true);
        _authServiceMock.Setup(x => x.Username).Returns("AdminUser");
        _authServiceMock.Setup(x => x.GetClientUserIdAsync()).ReturnsAsync("AdminUser");
        _authServiceMock.Setup(x => x.IsAdmin).Returns(true);

        var channels = new List<Channel>
        {
            new() { Id = Guid.NewGuid(), Name = "general", CreatedBy = "OtherUser", CreatedAt = DateTime.UtcNow }
        };

        _mockHttp.When(HttpMethod.Get, "*/api/channels")
            .Respond(HttpStatusCode.OK, request => JsonContent.Create(channels));
        _mockHttp.When(HttpMethod.Get, "*/api/messages/*")
            .Respond(HttpStatusCode.OK, request => JsonContent.Create(new List<Message>()));

        _chatServiceMock.Setup(x => x.InitializeAsync(It.IsAny<IHubConnectionBuilder>()))
            .Returns(Task.CompletedTask);
        _chatServiceMock.Setup(x => x.JoinChannel(It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        _privateMessageServiceMock.Setup(x => x.GetConversationsAsync("AdminUser"))
            .ReturnsAsync([]);

        var cut = await RenderChatAsync(channelName: "general");
        cut.Render();

        _authServiceMock.Verify(x => x.IsAdmin, Times.AtLeastOnce);
    }

    [Fact]
    public async Task Chat_CanManageCurrentChannel_ChannelCreator_ShouldReturnTrue()
    {
        _authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);
        _authServiceMock.Setup(x => x.HasUsername).Returns(true);
        _authServiceMock.Setup(x => x.Username).Returns("Creator");
        _authServiceMock.Setup(x => x.GetClientUserIdAsync()).ReturnsAsync("Creator");
        _authServiceMock.Setup(x => x.IsAdmin).Returns(false);

        var channels = new List<Channel>
        {
            new() { Id = Guid.NewGuid(), Name = "my-channel", CreatedBy = "Creator", CreatedAt = DateTime.UtcNow }
        };

        _mockHttp.When(HttpMethod.Get, "*/api/channels")
            .Respond(HttpStatusCode.OK, request => JsonContent.Create(channels));
        _mockHttp.When(HttpMethod.Get, "*/api/my-channels?username=Creator")
            .Respond(HttpStatusCode.OK, request => JsonContent.Create(channels));
        _mockHttp.When(HttpMethod.Get, "*/api/channels/my-channel/users")
            .Respond(HttpStatusCode.OK, request => JsonContent.Create(Array.Empty<User>()));
        _mockHttp.When(HttpMethod.Get, "*/api/messages/*")
            .Respond(HttpStatusCode.OK, request => JsonContent.Create(new List<Message>()));

        _chatServiceMock.Setup(x => x.InitializeAsync(It.IsAny<IHubConnectionBuilder>()))
            .Returns(Task.CompletedTask);
        _chatServiceMock.Setup(x => x.JoinChannel(It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        _privateMessageServiceMock.Setup(x => x.GetConversationsAsync("Creator"))
            .ReturnsAsync([]);

        var cut = await RenderChatAsync(channelName: "my-channel");

        _chatServiceMock.Verify(x => x.JoinChannel("my-channel"), Times.Once);
        Assert.Contains("Modifier la description", cut.Markup);
    }

    [Fact]
    public async Task Chat_SendMessage_WithValidContent_ShouldCallChatServiceWithCorrectParameters()
    {
        SetupBasicAuth();
        var channels = new List<Channel>
        {
            new() { Id = Guid.NewGuid(), Name = "general", CreatedBy = "system", CreatedAt = DateTime.UtcNow }
        };
        _mockHttp.When(HttpMethod.Get, "*/api/channels")
            .Respond(HttpStatusCode.OK, request => JsonContent.Create(channels));
        _mockHttp.When(HttpMethod.Get, "*/api/my-channels?username=TestUser")
            .Respond(HttpStatusCode.OK, request => JsonContent.Create(channels));
        _mockHttp.When(HttpMethod.Get, "*/api/messages/general")
            .Respond(HttpStatusCode.OK, request => JsonContent.Create(new List<Message>()));
        _mockHttp.When(HttpMethod.Get, "*/api/channels/general/users")
            .Respond(HttpStatusCode.OK, request => JsonContent.Create(new List<User>()));
        _chatServiceMock.Setup(x => x.InitializeAsync(It.IsAny<IHubConnectionBuilder>()))
            .Returns(Task.CompletedTask);
        _chatServiceMock.Setup(x => x.JoinChannel("general"))
            .Returns(Task.CompletedTask);
        _chatServiceMock.Setup(x => x.SendMessage(It.IsAny<SendMessageRequest>()))
            .Returns(Task.CompletedTask);
        _privateMessageServiceMock.Setup(x => x.GetConversationsAsync("TestUser"))
            .ReturnsAsync([]);

        var cut = await RenderChatAsync(channelName: "general");

        var input = cut.Find(".input-area input");
        var button = cut.Find(".input-area button");

        await cut.InvokeAsync(() => input.Input("Test message content"));
        await cut.InvokeAsync(() => button.Click());
        await Task.Delay(100);

        _chatServiceMock.Verify(
            x => x.SendMessage(It.Is<SendMessageRequest>(req =>
                req.Content == "Test message content" &&
                req.Channel == "general")),
            Times.Once);
    }

    [Fact]
    public async Task Chat_HandleSidebarToggle_WhenCalledWithFalse_ShouldCloseSidebar()
    {
        SetupBasicAuth();
        SetupBasicMocks();
        var cut = Render<Chat>();
        await Task.Delay(200);

        var toggleButton = cut.Find(".sidebar-toggle-btn");
        await cut.InvokeAsync(() => toggleButton.Click());
        await Task.Delay(100);

        Assert.Contains("sidebar-open", cut.Markup);

        await cut.InvokeAsync(() => toggleButton.Click());
        await Task.Delay(100);

        Assert.Contains("sidebar-closed", cut.Markup);
    }

    [Fact]
    public async Task Chat_LoadMessages_WhenApiCallFails_ShouldHandleGracefully()
    {
        SetupBasicAuth();
        var channels = new List<Channel>
        {
            new() { Id = Guid.NewGuid(), Name = "general", CreatedBy = "system", CreatedAt = DateTime.UtcNow }
        };

        _mockHttp.When(HttpMethod.Get, "*/api/channels")
            .Respond(HttpStatusCode.OK, request => JsonContent.Create(channels));
        _mockHttp.When(HttpMethod.Get, "*/api/my-channels?username=TestUser")
            .Respond(HttpStatusCode.OK, request => JsonContent.Create(Array.Empty<Channel>()));
        _mockHttp.When(HttpMethod.Get, "*/api/messages/general")
            .Respond(HttpStatusCode.InternalServerError);

        _chatServiceMock.Setup(x => x.InitializeAsync(It.IsAny<IHubConnectionBuilder>()))
            .Returns(Task.CompletedTask);
        _chatServiceMock.Setup(x => x.JoinChannel(It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        _privateMessageServiceMock.Setup(x => x.GetConversationsAsync(It.IsAny<string>()))
            .ReturnsAsync([]);

        var cut = await RenderChatAsync(channelName: "general");
        cut.Render();

        _chatServiceMock.Verify(x => x.JoinChannel(It.IsAny<string>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task Chat_LoadChannels_WhenApiCallFails_ShouldHandleGracefully()
    {
        SetupBasicAuth();
        _mockHttp.When(HttpMethod.Get, "*/api/channels")
            .Respond(HttpStatusCode.InternalServerError);

        _chatServiceMock.Setup(x => x.InitializeAsync(It.IsAny<IHubConnectionBuilder>()))
            .Returns(Task.CompletedTask);

        _privateMessageServiceMock.Setup(x => x.GetConversationsAsync(It.IsAny<string>()))
            .ReturnsAsync([]);

        var cut = Render<Chat>();
        await Task.Delay(200);

        Assert.NotNull(cut.Find(".chat-container"));
    }

    [Fact]
    public async Task Chat_CanManageCurrentChannel_WhenNoChannel_ShouldReturnFalse()
    {
        SetupBasicAuth();
        SetupBasicMocks();
        var cut = Render<Chat>();
        await Task.Delay(200);

        Assert.DoesNotContain("channel-mute-control", cut.Markup);
    }

    [Fact]
    public async Task Chat_DisposeAsync_ShouldUnsubscribeAllEvents()
    {
        SetupBasicAuth();
        SetupBasicMocks();
        var cut = Render<Chat>();
        await Task.Delay(200);

        await cut.Instance.DisposeAsync();

        _chatServiceMock.VerifyRemove(x => x.OnMessageReceived -= It.IsAny<Action<Message>>());
        _chatServiceMock.VerifyRemove(x => x.OnUserJoined -= It.IsAny<Action<string, string, string>>());
        _chatServiceMock.VerifyRemove(x => x.OnUserLeft -= It.IsAny<Action<string, string, string>>());
        _chatServiceMock.VerifyRemove(x => x.OnChannelMuteStatusChanged -= It.IsAny<Action<string, bool>>());
        _chatServiceMock.VerifyRemove(x => x.OnMessageBlocked -= It.IsAny<Action<string>>());
        _chatServiceMock.VerifyRemove(x => x.OnChannelDeleted -= It.IsAny<Action<string>>());
        _chatServiceMock.VerifyRemove(x => x.OnChannelNotFound -= It.IsAny<Action<string>>());
        _chatServiceMock.VerifyRemove(x => x.OnChannelListUpdated -= It.IsAny<Action>());

        _privateMessageServiceMock.VerifyRemove(x => x.OnPrivateMessageReceived -= It.IsAny<Action<PrivateMessage>>());
        _privateMessageServiceMock.VerifyRemove(x => x.OnPrivateMessageSent -= It.IsAny<Action<PrivateMessage>>());
        _privateMessageServiceMock.VerifyRemove(x => x.OnUnreadCountChanged -= It.IsAny<Action>());
        _privateMessageServiceMock.VerifyRemove(x => x.OnConversationDeleted -= It.IsAny<Action<string>>());
        _ignoredUsersServiceMock.VerifyRemove(x => x.OnIgnoredUsersChanged -= It.IsAny<Action>());
    }

    // ============ TESTS POUR USER MUTED ============

    [Fact]
    [SuppressMessage("Major Code Smell", "S6966:Awaitable method should be used", Justification = "Raise doesn't support async")]
    public async Task Chat_OnUserMuted_ShouldReloadUsersInCurrentChannel()
    {
        // Arrange
        SetupBasicAuth();
        var channels = new List<Channel>
        {
            new() { Id = Guid.NewGuid(), Name = "general", CreatedBy = "system", CreatedAt = DateTime.UtcNow }
        };
        var initialUsers = new List<User>
        {
            new() { UserId = "user1", Username = "Alice" },
            new() { UserId = "user2", Username = "Bob" }
        };

        _mockHttp.When(HttpMethod.Get, "*/api/channels")
            .Respond(HttpStatusCode.OK, request => JsonContent.Create(channels));
        _mockHttp.When(HttpMethod.Get, "*/api/messages/general")
            .Respond(HttpStatusCode.OK, request => JsonContent.Create(new List<Message>()));

        // Première requête pour LoadUsers initial
        var getUsersRequest = _mockHttp
            .When(HttpMethod.Get, "*/api/channels/general/users")
            .Respond(HttpStatusCode.OK, request => JsonContent.Create(initialUsers));

        _chatServiceMock.Setup(x => x.InitializeAsync(It.IsAny<IHubConnectionBuilder>()))
            .Returns(Task.CompletedTask);
        _chatServiceMock.Setup(x => x.JoinChannel("general")).Returns(Task.CompletedTask);
        _privateMessageServiceMock.Setup(x => x.GetConversationsAsync(It.IsAny<string>()))
            .ReturnsAsync([]);

        var cut = await RenderChatAsync(channelName: "general");

        // Act - Lever l'événement UserMuted
        _chatServiceMock.Raise(x => x.OnUserMuted += null, "general", "user1", "Alice", "admin-id", "Admin");
        await Task.Delay(200);
        cut.Render();

        // Assert
        Assert.Equal(2, _mockHttp.GetMatchCount(getUsersRequest));
    }

    [Fact]
    [SuppressMessage("Major Code Smell", "S6966:Awaitable method should be used", Justification = "Raise doesn't support async")]
    public async Task Chat_OnUserMuted_WhenDifferentChannel_ShouldNotReload()
    {
        // Arrange
        SetupBasicAuth();
        var channels = new List<Channel>
        {
            new() { Id = Guid.NewGuid(), Name = "general", CreatedBy = "system", CreatedAt = DateTime.UtcNow },
            new() { Id = Guid.NewGuid(), Name = "random", CreatedBy = "system", CreatedAt = DateTime.UtcNow }
        };
        var users = new List<User>
        {
            new() { UserId = "user1", Username = "Alice" }
        };

        _mockHttp.When(HttpMethod.Get, "*/api/channels")
            .Respond(HttpStatusCode.OK, request => JsonContent.Create(channels));
        _mockHttp.When(HttpMethod.Get, "*/api/my-channels?username=TestUser")
            .Respond(HttpStatusCode.OK, request => JsonContent.Create(channels));
        _mockHttp.When(HttpMethod.Get, "*/api/messages/general")
            .Respond(HttpStatusCode.OK, request => JsonContent.Create(new List<Message>()));
        _mockHttp.When(HttpMethod.Get, "*/api/messages/random")
            .Respond(HttpStatusCode.OK, request => JsonContent.Create(new List<Message>()));

        var getUsersRequest = _mockHttp
            .When(HttpMethod.Get, "*/api/channels/general/users")
            .Respond(HttpStatusCode.OK, request => JsonContent.Create(users));

        _chatServiceMock.Setup(x => x.InitializeAsync(It.IsAny<IHubConnectionBuilder>()))
            .Returns(Task.CompletedTask);
        _chatServiceMock.Setup(x => x.JoinChannel(It.IsAny<string>())).Returns(Task.CompletedTask);
        _privateMessageServiceMock.Setup(x => x.GetConversationsAsync(It.IsAny<string>()))
            .ReturnsAsync([]);

        await RenderChatAsync(channelName: "general");

        // Act - Lever l'événement UserMuted pour un autre canal
        _chatServiceMock.Raise(x => x.OnUserMuted += null, "random", "user1", "Alice", "admin-id", "Admin");
        await Task.Delay(200);

        // Assert - Devrait ne pas recharger (count = 1 = seulement l'initial)
        Assert.Equal(1, _mockHttp.GetMatchCount(getUsersRequest));
    }

    [Fact]
    [SuppressMessage("Major Code Smell", "S6966:Awaitable method should be used", Justification = "Raise doesn't support async")]
    public async Task Chat_OnUserMuted_WhenPrivateConversation_ShouldNotReload()
    {
        // Arrange
        SetupBasicAuth();
        _mockHttp.When(HttpMethod.Get, "*/api/channels")
            .Respond(HttpStatusCode.OK, request => JsonContent.Create(new List<Channel>()));
        _mockHttp.When(HttpMethod.Get, "*/api/private-messages/status/Friend")
            .Respond(HttpStatusCode.OK, request => JsonContent.Create(new { Username = "Friend", IsOnline = true }));

        var getUsersRequest = _mockHttp
            .When(HttpMethod.Get, "*/api/channels/*/users")
            .Respond(HttpStatusCode.OK, request => JsonContent.Create(new List<User>()));

        _chatServiceMock.Setup(x => x.InitializeAsync(It.IsAny<IHubConnectionBuilder>()))
            .Returns(Task.CompletedTask);
        _chatServiceMock.Setup(x => x.MarkPrivateMessagesAsRead(It.IsAny<string>()))
            .Returns(Task.CompletedTask);
        _privateMessageServiceMock.Setup(x => x.GetConversationsAsync("TestUser"))
            .ReturnsAsync([new() { OtherUser = new User { UserId = "Friend", Username = "Friend" }, LastMessage = "Hi", LastMessageTime = DateTime.UtcNow, UnreadCount = 0 }]);
        _privateMessageServiceMock.Setup(x => x.GetPrivateMessagesAsync("TestUser", "Friend"))
            .ReturnsAsync([]);

        var cut = await RenderChatAsync();
        await NavigateToPrivateChatAsync(cut, "Friend", "Friend");

        // Act - Lever l'événement UserMuted (même avec channel="general")
        _chatServiceMock.Raise(x => x.OnUserMuted += null, "general", "user1", "Alice", "admin-id", "Admin");
        await Task.Delay(200);

        // Assert - Devrait ne pas faire de requête GetUsers car en conversation privée
        Assert.Equal(0, _mockHttp.GetMatchCount(getUsersRequest));
    }

    [Fact]
    [SuppressMessage("Major Code Smell", "S6966:Awaitable method should be used", Justification = "Raise doesn't support async")]
    public async Task Chat_OnUserUnmuted_ShouldReloadUsersInCurrentChannel()
    {
        // Arrange
        SetupBasicAuth();
        var channels = new List<Channel>
        {
            new() { Id = Guid.NewGuid(), Name = "general", CreatedBy = "system", CreatedAt = DateTime.UtcNow }
        };
        var users = new List<User>
        {
            new() { UserId = "user1", Username = "Alice" },
            new() { UserId = "user2", Username = "Bob" }
        };

        _mockHttp.When(HttpMethod.Get, "*/api/channels")
            .Respond(HttpStatusCode.OK, request => JsonContent.Create(channels));
        _mockHttp.When(HttpMethod.Get, "*/api/messages/general")
            .Respond(HttpStatusCode.OK, request => JsonContent.Create(new List<Message>()));

        var getUsersRequest = _mockHttp
            .When(HttpMethod.Get, "*/api/channels/general/users")
            .Respond(HttpStatusCode.OK, request => JsonContent.Create(users));

        _chatServiceMock.Setup(x => x.InitializeAsync(It.IsAny<IHubConnectionBuilder>()))
            .Returns(Task.CompletedTask);
        _chatServiceMock.Setup(x => x.JoinChannel("general")).Returns(Task.CompletedTask);
        _privateMessageServiceMock.Setup(x => x.GetConversationsAsync(It.IsAny<string>()))
            .ReturnsAsync([]);

        var cut = await RenderChatAsync(channelName: "general");

        // Act - Lever l'événement UserUnmuted
        _chatServiceMock.Raise(x => x.OnUserUnmuted += null, "general", "user1", "Alice", "admin-id", "Admin");
        await Task.Delay(200);
        cut.Render();

        // Assert
        Assert.Equal(2, _mockHttp.GetMatchCount(getUsersRequest));
    }

    [Fact]
    [SuppressMessage("Major Code Smell", "S6966:Awaitable method should be used", Justification = "Raise doesn't support async")]
    public async Task Chat_OnUserUnmuted_WhenDifferentChannel_ShouldNotReload()
    {
        // Arrange
        SetupBasicAuth();
        var channels = new List<Channel>
        {
            new() { Id = Guid.NewGuid(), Name = "general", CreatedBy = "system", CreatedAt = DateTime.UtcNow },
            new() { Id = Guid.NewGuid(), Name = "random", CreatedBy = "system", CreatedAt = DateTime.UtcNow }
        };
        var users = new List<User>
        {
            new() { UserId = "user1", Username = "Alice" }
        };

        _mockHttp.When(HttpMethod.Get, "*/api/channels")
            .Respond(HttpStatusCode.OK, request => JsonContent.Create(channels));
        _mockHttp.When(HttpMethod.Get, "*/api/my-channels?username=TestUser")
            .Respond(HttpStatusCode.OK, request => JsonContent.Create(channels));
        _mockHttp.When(HttpMethod.Get, "*/api/messages/general")
            .Respond(HttpStatusCode.OK, request => JsonContent.Create(new List<Message>()));
        _mockHttp.When(HttpMethod.Get, "*/api/messages/random")
            .Respond(HttpStatusCode.OK, request => JsonContent.Create(new List<Message>()));

        var getUsersRequest = _mockHttp
            .When(HttpMethod.Get, "*/api/channels/general/users")
            .Respond(HttpStatusCode.OK, request => JsonContent.Create(users));

        _chatServiceMock.Setup(x => x.InitializeAsync(It.IsAny<IHubConnectionBuilder>()))
            .Returns(Task.CompletedTask);
        _chatServiceMock.Setup(x => x.JoinChannel(It.IsAny<string>())).Returns(Task.CompletedTask);
        _privateMessageServiceMock.Setup(x => x.GetConversationsAsync(It.IsAny<string>()))
            .ReturnsAsync([]);

        await RenderChatAsync(channelName: "general");

        // Act - Lever l'événement UserUnmuted pour un autre canal
        _chatServiceMock.Raise(x => x.OnUserUnmuted += null, "random", "user1", "Alice", "admin-id", "Admin");
        await Task.Delay(200);

        // Assert
        Assert.Equal(1, _mockHttp.GetMatchCount(getUsersRequest));
    }

    [Fact]
    [SuppressMessage("Major Code Smell", "S6966:Awaitable method should be used", Justification = "Raise doesn't support async")]
    public async Task Chat_OnUserUnmuted_WhenPrivateConversation_ShouldNotReload()
    {
        // Arrange
        SetupBasicAuth();
        _mockHttp.When(HttpMethod.Get, "*/api/channels")
            .Respond(HttpStatusCode.OK, request => JsonContent.Create(new List<Channel>()));
        _mockHttp.When(HttpMethod.Get, "*/api/private-messages/status/Friend")
            .Respond(HttpStatusCode.OK, request => JsonContent.Create(new { Username = "Friend", IsOnline = true }));

        var getUsersRequest = _mockHttp
            .When(HttpMethod.Get, "*/api/channels/*/users")
            .Respond(HttpStatusCode.OK, request => JsonContent.Create(new List<User>()));

        _chatServiceMock.Setup(x => x.InitializeAsync(It.IsAny<IHubConnectionBuilder>()))
            .Returns(Task.CompletedTask);
        _chatServiceMock.Setup(x => x.MarkPrivateMessagesAsRead(It.IsAny<string>()))
            .Returns(Task.CompletedTask);
        _privateMessageServiceMock.Setup(x => x.GetConversationsAsync("TestUser"))
            .ReturnsAsync([new() { OtherUser = new User { UserId = "Friend", Username = "Friend" }, LastMessage = "Hi", LastMessageTime = DateTime.UtcNow, UnreadCount = 0 }]);
        _privateMessageServiceMock.Setup(x => x.GetPrivateMessagesAsync("TestUser", "Friend"))
            .ReturnsAsync([]);

        var cut = await RenderChatAsync();
        await NavigateToPrivateChatAsync(cut, "Friend", "Friend");

        // Act - Lever l'événement UserUnmuted
        _chatServiceMock.Raise(x => x.OnUserUnmuted += null, "general", "user1", "Alice", "admin-id", "Admin");
        await Task.Delay(200);

        // Assert
        Assert.Equal(0, _mockHttp.GetMatchCount(getUsersRequest));
    }

    [Fact]
    public async Task Chat_DisposeAsync_ShouldUnsubscribeFromUserMutedAndUnmutedEvents()
    {
        // Arrange
        SetupBasicAuth();
        SetupBasicMocks();
        var cut = Render<Chat>();
        await Task.Delay(200);

        // Act
        await cut.Instance.DisposeAsync();

        // Assert
        _chatServiceMock.VerifyRemove(x => x.OnUserMuted -= It.IsAny<Action<string, string, string, string, string>>());
        _chatServiceMock.VerifyRemove(x => x.OnUserUnmuted -= It.IsAny<Action<string, string, string, string, string>>());
    }

    [Fact]
    public async Task Chat_InitializeSignalR_ShouldSubscribeToUserMutedAndUnmutedEvents()
    {
        // Arrange
        SetupBasicAuth();
        _mockHttp.When(HttpMethod.Get, "*/api/channels")
            .Respond(HttpStatusCode.OK, request => JsonContent.Create(new List<Channel>()));
        _chatServiceMock.Setup(x => x.InitializeAsync(It.IsAny<IHubConnectionBuilder>()))
            .Returns(Task.CompletedTask);
        _privateMessageServiceMock.Setup(x => x.GetConversationsAsync(It.IsAny<string>()))
            .ReturnsAsync([]);

        // Act
        Render<Chat>();
        await Task.Delay(200);

        // Assert
        _chatServiceMock.VerifyAdd(x => x.OnUserMuted += It.IsAny<Action<string, string, string, string, string>>());
        _chatServiceMock.VerifyAdd(x => x.OnUserUnmuted += It.IsAny<Action<string, string, string, string, string>>());
    }

    [Fact]
    [SuppressMessage("Major Code Smell", "S6966:Awaitable method should be used", Justification = "Raise doesn't support async")]
    public async Task Chat_OnUserMuted_ShouldLogWithCorrectParameters()
    {
        // Arrange
        SetupBasicAuth();
        var channels = new List<Channel>
        {
            new() { Id = Guid.NewGuid(), Name = "general", CreatedBy = "system", CreatedAt = DateTime.UtcNow }
        };
        var users = new List<User>
        {
            new() { UserId = "user1", Username = "Alice" }
        };

        _mockHttp.When(HttpMethod.Get, "*/api/channels")
            .Respond(HttpStatusCode.OK, request => JsonContent.Create(channels));
        _mockHttp.When(HttpMethod.Get, "*/api/messages/general")
            .Respond(HttpStatusCode.OK, request => JsonContent.Create(new List<Message>()));
        _mockHttp.When(HttpMethod.Get, "*/api/channels/general/users")
            .Respond(HttpStatusCode.OK, request => JsonContent.Create(users));

        _chatServiceMock.Setup(x => x.InitializeAsync(It.IsAny<IHubConnectionBuilder>()))
            .Returns(Task.CompletedTask);
        _chatServiceMock.Setup(x => x.JoinChannel("general")).Returns(Task.CompletedTask);
        _privateMessageServiceMock.Setup(x => x.GetConversationsAsync(It.IsAny<string>()))
            .ReturnsAsync([]);

        await RenderChatAsync(channelName: "general");

        // Act - Lever l'événement avec des paramètres spécifiques
        _chatServiceMock.Raise(x => x.OnUserMuted += null, "general", "alice-id", "Alice", "admin-id", "Admin");
        await Task.Delay(200);

        // Assert - Vérifier que l'événement a été levé avec les bons paramètres
        _chatServiceMock.VerifyAdd(x => x.OnUserMuted += It.IsAny<Action<string, string, string, string, string>>());
    }

    [Fact]
    [SuppressMessage("Major Code Smell", "S6966:Awaitable method should be used", Justification = "Raise doesn't support async")]
    public async Task Chat_OnUserUnmuted_ShouldLogWithCorrectParameters()
    {
        // Arrange
        SetupBasicAuth();
        var channels = new List<Channel>
        {
            new() { Id = Guid.NewGuid(), Name = "general", CreatedBy = "system", CreatedAt = DateTime.UtcNow }
        };
        var users = new List<User>
        {
            new() { UserId = "user1", Username = "Alice" }
        };

        _mockHttp.When(HttpMethod.Get, "*/api/channels")
            .Respond(HttpStatusCode.OK, request => JsonContent.Create(channels));
        _mockHttp.When(HttpMethod.Get, "*/api/messages/general")
            .Respond(HttpStatusCode.OK, request => JsonContent.Create(new List<Message>()));
        _mockHttp.When(HttpMethod.Get, "*/api/channels/general/users")
            .Respond(HttpStatusCode.OK, request => JsonContent.Create(users));

        _chatServiceMock.Setup(x => x.InitializeAsync(It.IsAny<IHubConnectionBuilder>()))
            .Returns(Task.CompletedTask);
        _chatServiceMock.Setup(x => x.JoinChannel("general")).Returns(Task.CompletedTask);
        _privateMessageServiceMock.Setup(x => x.GetConversationsAsync(It.IsAny<string>()))
            .ReturnsAsync([]);

        await RenderChatAsync(channelName: "general");

        // Act - Lever l'événement avec des paramètres spécifiques
        _chatServiceMock.Raise(x => x.OnUserUnmuted += null, "general", "alice-id", "Alice", "admin-id", "Admin");
        await Task.Delay(200);

        // Assert
        _chatServiceMock.VerifyAdd(x => x.OnUserUnmuted += It.IsAny<Action<string, string, string, string, string>>());
    }

    // ============ TESTS POUR LES NOTIFICATIONS DE CONNEXION ============

    [Fact]
    public async Task Chat_OnInitialization_ShouldSubscribeToConnectionEvents()
    {
        // Arrange
        SetupBasicAuth();
        SetupBasicMocks();

        // Act
        Render<Chat>();
        await Task.Delay(200);

        // Assert
        _chatServiceMock.VerifyAdd(x => x.OnDisconnected += It.IsAny<Action>());
        _chatServiceMock.VerifyAdd(x => x.OnReconnecting += It.IsAny<Action<string?>>());
        _chatServiceMock.VerifyAdd(x => x.OnReconnected += It.IsAny<Action>());
    }

    [Fact]
    [SuppressMessage("Major Code Smell", "S6966:Awaitable method should be used", Justification = "Raise doesn't support async")]
    public async Task Chat_OnSignalRDisconnected_ShouldShowDisconnectedNotification()
    {
        // Arrange
        SetupBasicAuth();
        SetupBasicMocks();
        var cut = Render<Chat>();
        await Task.Delay(200);

        // Act
        _chatServiceMock.Raise(x => x.OnDisconnected += null);
        await Task.Delay(200);
        cut.Render();

        // Assert
        Assert.Contains("connection-notification", cut.Markup);
        Assert.Contains("error", cut.Markup);
        Assert.Contains("Connexion perdue", cut.Markup);
    }

    [Fact]
    [SuppressMessage("Major Code Smell", "S6966:Awaitable method should be used", Justification = "Raise doesn't support async")]
    public async Task Chat_OnSignalRReconnecting_ShouldShowReconnectingNotification()
    {
        // Arrange
        SetupBasicAuth();
        SetupBasicMocks();
        var cut = Render<Chat>();
        await Task.Delay(200);

        // Act
        _chatServiceMock.Raise(x => x.OnReconnecting += null, (string?)null!);
        await Task.Delay(200);
        cut.Render();

        // Assert
        Assert.Contains("connection-notification", cut.Markup);
        Assert.Contains("warning", cut.Markup);
        Assert.Contains("Reconnexion en cours", cut.Markup);
    }

    [Fact]
    [SuppressMessage("Major Code Smell", "S6966:Awaitable method should be used", Justification = "Raise doesn't support async")]
    public async Task Chat_OnSignalRReconnecting_WithError_ShouldShowErrorMessage()
    {
        // Arrange
        SetupBasicAuth();
        SetupBasicMocks();
        var cut = Render<Chat>();
        await Task.Delay(200);

        // Act
        _chatServiceMock.Raise(x => x.OnReconnecting += null, "Connection timeout");
        await Task.Delay(200);
        cut.Render();

        // Assert
        Assert.Contains("connection-notification", cut.Markup);
        Assert.Contains("warning", cut.Markup);
        Assert.Contains("Reconnexion en cours", cut.Markup);
        Assert.Contains("Connection timeout", cut.Markup);
    }

    [Fact]
    [SuppressMessage("Major Code Smell", "S6966:Awaitable method should be used", Justification = "Raise doesn't support async")]
    public async Task Chat_OnSignalRReconnected_ShouldShowSuccessNotification()
    {
        // Arrange
        SetupBasicAuth();
        SetupBasicMocks();
        var cut = Render<Chat>();
        await Task.Delay(200);

        // Act
        _chatServiceMock.Raise(x => x.OnReconnected += null);
        await Task.Delay(200);
        cut.Render();

        // Assert
        Assert.Contains("connection-notification", cut.Markup);
        Assert.Contains("success", cut.Markup);
        Assert.Contains("Reconnexion réussie", cut.Markup);
    }

    [Fact]
    [SuppressMessage("Major Code Smell", "S6966:Awaitable method should be used", Justification = "Raise doesn't support async")]
    public async Task Chat_OnSignalRReconnected_ShouldRestoreChannels()
    {
        // Arrange
        SetupBasicAuth();
        var channels = new List<Channel>
        {
            new() { Id = Guid.NewGuid(), Name = "general", CreatedBy = "system", CreatedAt = DateTime.UtcNow }
        };

        _mockHttp.When(HttpMethod.Get, "*/api/channels")
            .Respond(HttpStatusCode.OK, request => JsonContent.Create(channels));
        _chatServiceMock.Setup(x => x.InitializeAsync(It.IsAny<IHubConnectionBuilder>()))
            .Returns(Task.CompletedTask);
        _privateMessageServiceMock.Setup(x => x.GetConversationsAsync(It.IsAny<string>()))
            .ReturnsAsync([]);
        _activeChannelsServiceMock.Setup(x => x.GetActiveChannelsAsync()).ReturnsAsync([.. channels.Select(c => c.Name)]);
        Render<Chat>();
        await Task.Delay(200);

        // Act
        _chatServiceMock.Raise(x => x.OnReconnected += null);
        await Task.Delay(300);

        // Assert - Devrait charger les canaux 2 fois : initial + après reconnexion
        _activeChannelsServiceMock.Verify(x => x.GetActiveChannelsAsync(), Times.Exactly(2));
    }

    [Fact]
    [SuppressMessage("Major Code Smell", "S6966:Awaitable method should be used", Justification = "Raise doesn't support async")]
    public async Task Chat_OnSignalRReconnected_WhenInChannel_ShouldReloadMessagesAndUsers()
    {
        // Arrange
        SetupBasicAuth();
        var channels = new List<Channel>
        {
            new() { Id = Guid.NewGuid(), Name = "general", CreatedBy = "system", CreatedAt = DateTime.UtcNow }
        };
        var messages = new List<Message>
        {
            new() { Id = Guid.NewGuid(), Username = "User1", Content = "Hello", Channel = "general", Timestamp = DateTime.UtcNow }
        };
        var users = new List<User>
        {
            new() { UserId = "user1", Username = "User1" }
        };

        _mockHttp.When(HttpMethod.Get, "*/api/channels")
            .Respond(HttpStatusCode.OK, request => JsonContent.Create(channels));

        var messagesRequest = _mockHttp.When(HttpMethod.Get, "*/api/messages/general*")
            .Respond(HttpStatusCode.OK, request => JsonContent.Create(messages));

        var usersRequest = _mockHttp.When(HttpMethod.Get, "*/api/channels/general/users")
            .Respond(HttpStatusCode.OK, request => JsonContent.Create(users));

        _chatServiceMock.Setup(x => x.InitializeAsync(It.IsAny<IHubConnectionBuilder>()))
            .Returns(Task.CompletedTask);
        _chatServiceMock.Setup(x => x.JoinChannel("general")).Returns(Task.CompletedTask);
        _privateMessageServiceMock.Setup(x => x.GetConversationsAsync(It.IsAny<string>()))
            .ReturnsAsync([]);

        await RenderChatAsync(channelName: "general");

        // Act
        _chatServiceMock.Raise(x => x.OnReconnected += null);
        await Task.Delay(300);

        // Assert - Devrait charger messages et users 2 fois : initial + après reconnexion
        Assert.Equal(2, _mockHttp.GetMatchCount(messagesRequest));
        Assert.Equal(2, _mockHttp.GetMatchCount(usersRequest));
    }

    [Fact]
    [SuppressMessage("Major Code Smell", "S6966:Awaitable method should be used", Justification = "Raise doesn't support async")]
    public async Task Chat_OnSignalRReconnected_WhenInPrivateChat_ShouldReloadPrivateMessages()
    {
        // Arrange
        SetupBasicAuth();
        _mockHttp.When(HttpMethod.Get, "*/api/channels")
            .Respond(HttpStatusCode.OK, request => JsonContent.Create(new List<Channel>()));
        _mockHttp.When(HttpMethod.Get, "*/api/private-messages/status/Friend")
            .Respond(HttpStatusCode.OK, request => JsonContent.Create(new { Username = "Friend", IsOnline = true }));

        _chatServiceMock.Setup(x => x.InitializeAsync(It.IsAny<IHubConnectionBuilder>()))
            .Returns(Task.CompletedTask);
        _chatServiceMock.Setup(x => x.MarkPrivateMessagesAsRead(It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        var conversations = new List<PrivateConversation>
        {
            new() { OtherUser = new User { UserId = "Friend", Username = "Friend" }, LastMessage = "Hi", LastMessageTime = DateTime.UtcNow, UnreadCount = 0 }
        };
        _privateMessageServiceMock.Setup(x => x.GetConversationsAsync("TestUser"))
            .ReturnsAsync(conversations);
        _privateMessageServiceMock.Setup(x => x.GetPrivateMessagesAsync("TestUser", "Friend"))
            .ReturnsAsync([]);

        var cut = await RenderChatAsync();
        await NavigateToPrivateChatAsync(cut, "Friend", "Friend");

        _privateMessageServiceMock.Invocations.Clear();

        // Act
        _chatServiceMock.Raise(x => x.OnReconnected += null);
        await Task.Delay(300);

        // Assert
        _privateMessageServiceMock.Verify(x => x.GetPrivateMessagesAsync("TestUser", "Friend"), Times.AtLeastOnce);
    }

    [Fact]
    [SuppressMessage("Major Code Smell", "S6966:Awaitable method should be used", Justification = "Raise doesn't support async")]
    public async Task Chat_OnSignalRReconnected_ShouldReloadPrivateConversations()
    {
        // Arrange
        SetupBasicAuth();
        SetupBasicMocks();
        var conversations = new List<PrivateConversation>
        {
            new() { OtherUser = new User { UserId = "User2", Username = "User2" }, LastMessage = "Hello", LastMessageTime = DateTime.UtcNow, UnreadCount = 1 }
        };
        _privateMessageServiceMock.Setup(x => x.GetConversationsAsync("TestUser"))
            .ReturnsAsync(conversations);

        Render<Chat>();
        await Task.Delay(200);

        _privateMessageServiceMock.Invocations.Clear();

        // Act
        _chatServiceMock.Raise(x => x.OnReconnected += null);
        await Task.Delay(300);

        // Assert
        _privateMessageServiceMock.Verify(x => x.GetConversationsAsync("TestUser"), Times.AtLeastOnce);
    }

    [Fact]
    [SuppressMessage("Major Code Smell", "S6966:Awaitable method should be used", Justification = "Raise doesn't support async")]
    public async Task Chat_OnSignalRDisconnected_ShouldSetIsConnectedToFalse()
    {
        // Arrange
        SetupBasicAuth();
        SetupBasicMocks();
        var isConnected = true;
        _chatServiceMock.SetupGet(x => x.IsInitialized).Returns(() => isConnected);

        var cut = await RenderChatAsync(channelName: "general");
        cut.Render();
        // Vérifier que isConnected est true initialement
        Assert.Contains("●", cut.Markup);
        Assert.Contains("Connecté", cut.Markup);

        // Act
        _chatServiceMock.Raise(x => x.OnDisconnected += null);
        isConnected = false;
        await Task.Delay(200);
        cut.Render();

        // Assert - Le statut devrait être déconnecté
        Assert.Contains("○", cut.Markup);
        Assert.Contains("Déconnecté", cut.Markup);
    }

    [Fact]
    [SuppressMessage("Major Code Smell", "S6966:Awaitable method should be used", Justification = "Raise doesn't support async")]
    public async Task Chat_OnSignalRReconnected_ShouldSetIsConnectedToTrue()
    {
        // Arrange
        SetupBasicAuth();
        SetupBasicMocks();
        var isConnected = true;
        _chatServiceMock.SetupGet(x => x.IsInitialized).Returns(() => isConnected);
        var cut = await RenderChatAsync(channelName: "general");

        // Simuler une déconnexion
        _chatServiceMock.Raise(x => x.OnDisconnected += null);
        isConnected = false;
        await Task.Delay(200);
        cut.Render();

        // Act
        _chatServiceMock.Raise(x => x.OnReconnected += null);
        isConnected = true;
        await Task.Delay(200);
        cut.Render();

        // Assert - Le statut devrait être reconnecté
        Assert.Contains("●", cut.Markup);
        Assert.Contains("Connecté", cut.Markup);
    }

    [Fact]
    [SuppressMessage("Major Code Smell", "S6966:Awaitable method should be used", Justification = "Raise doesn't support async")]
    public async Task Chat_ConnectionNotification_ShouldAutoHideAfter4Seconds()
    {
        // Arrange
        SetupBasicAuth();
        SetupBasicMocks();
        var cut = Render<Chat>();
        await Task.Delay(200);

        // Act
        _chatServiceMock.Raise(x => x.OnReconnected += null);
        await Task.Delay(200);
        cut.Render();

        // Assert - La notification devrait être visible
        Assert.Contains("connection-notification", cut.Markup);
        Assert.Contains("Reconnexion réussie", cut.Markup);

        // Wait for auto-hide (4 seconds + buffer)
        await Task.Delay(4500);
        cut.Render();

        // Assert - La notification devrait avoir disparu
        Assert.DoesNotContain("Reconnexion réussie", cut.Markup);
    }

    [Fact]
    [SuppressMessage("Major Code Smell", "S6966:Awaitable method should be used", Justification = "Raise doesn't support async")]
    public async Task Chat_ReconnectingNotification_ShouldBePersistent()
    {
        // Arrange
        SetupBasicAuth();
        SetupBasicMocks();
        var cut = Render<Chat>();
        await Task.Delay(200);

        // Act
        _chatServiceMock.Raise(x => x.OnReconnecting += null, (string?)null!);
        await Task.Delay(200);
        cut.Render();

        // Assert - La notification devrait être visible
        Assert.Contains("connection-notification", cut.Markup);
        Assert.Contains("Reconnexion en cours", cut.Markup);

        // Wait more than 4 seconds
        await Task.Delay(4500);
        cut.Render();

        // Assert - La notification devrait toujours être visible (persistante)
        Assert.Contains("Reconnexion en cours", cut.Markup);
    }

    [Fact]
    public async Task Chat_DisposeAsync_ShouldUnsubscribeFromConnectionEvents()
    {
        // Arrange
        SetupBasicAuth();
        SetupBasicMocks();
        var cut = Render<Chat>();
        await Task.Delay(200);

        // Act
        await cut.Instance.DisposeAsync();

        // Assert
        _chatServiceMock.VerifyRemove(x => x.OnDisconnected -= It.IsAny<Action>());
        _chatServiceMock.VerifyRemove(x => x.OnReconnecting -= It.IsAny<Action<string?>>());
        _chatServiceMock.VerifyRemove(x => x.OnReconnected -= It.IsAny<Action>());
    }

    [Fact]
    [SuppressMessage("Major Code Smell", "S6966:Awaitable method should be used", Justification = "Raise doesn't support async")]
    public async Task Chat_MultipleDisconnections_ShouldUpdateNotification()
    {
        // Arrange
        SetupBasicAuth();
        SetupBasicMocks();
        var cut = Render<Chat>();
        await Task.Delay(200);

        // Act - Première déconnexion
        _chatServiceMock.Raise(x => x.OnDisconnected += null);
        await Task.Delay(200);
        cut.Render();

        Assert.Contains("Connexion perdue", cut.Markup);

        // Act - Tentative de reconnexion
        _chatServiceMock.Raise(x => x.OnReconnecting += null, (string?)null!);
        await Task.Delay(200);
        cut.Render();

        Assert.Contains("Reconnexion en cours", cut.Markup);

        // Act - Reconnexion réussie
        _chatServiceMock.Raise(x => x.OnReconnected += null);
        await Task.Delay(200);
        cut.Render();

        // Assert
        Assert.Contains("Reconnexion réussie", cut.Markup);
    }

    [Fact]
    [SuppressMessage("Major Code Smell", "S6966:Awaitable method should be used", Justification = "Raise doesn't support async")]
    public async Task Chat_OnSignalRReconnected_WhenNotInChannelOrPrivateChat_ShouldNotReloadMessages()
    {
        // Arrange
        SetupBasicAuth();
        var channels = new List<Channel>
        {
            new() { Id = Guid.NewGuid(), Name = "general", CreatedBy = "system", CreatedAt = DateTime.UtcNow }
        };

        _mockHttp.When(HttpMethod.Get, "*/api/channels")
            .Respond(HttpStatusCode.OK, request => JsonContent.Create(channels));

        var messagesRequest = _mockHttp.When(HttpMethod.Get, "*/api/messages/*")
            .Respond(HttpStatusCode.OK, request => JsonContent.Create(new List<Message>()));

        var usersRequest = _mockHttp.When(HttpMethod.Get, "*/api/channels/*/users")
            .Respond(HttpStatusCode.OK, request => JsonContent.Create(new List<User>()));

        _chatServiceMock.Setup(x => x.InitializeAsync(It.IsAny<IHubConnectionBuilder>()))
            .Returns(Task.CompletedTask);
        _privateMessageServiceMock.Setup(x => x.GetConversationsAsync(It.IsAny<string>()))
            .ReturnsAsync([]);

        Render<Chat>();
        await Task.Delay(200);

        // Act - Reconnexion sans être dans un canal
        _chatServiceMock.Raise(x => x.OnReconnected += null);
        await Task.Delay(300);

        // Assert - Ne devrait pas charger de messages ou d'utilisateurs
        Assert.Equal(0, _mockHttp.GetMatchCount(messagesRequest));
        Assert.Equal(0, _mockHttp.GetMatchCount(usersRequest));
    }

    [Fact]
    [SuppressMessage("Major Code Smell", "S6966:Awaitable method should be used", Justification = "Raise doesn't support async")]
    public async Task Chat_ConnectionNotificationClass_ShouldChangeBasedOnType()
    {
        // Arrange
        SetupBasicAuth();
        SetupBasicMocks();
        var cut = Render<Chat>();
        await Task.Delay(200);

        // Test Disconnected
        _chatServiceMock.Raise(x => x.OnDisconnected += null);
        await Task.Delay(200);
        cut.Render();
        Assert.Contains("error", cut.Markup);

        // Test Reconnecting
        _chatServiceMock.Raise(x => x.OnReconnecting += null, (string?)null!);
        await Task.Delay(200);
        cut.Render();
        Assert.Contains("warning", cut.Markup);

        // Test Reconnected
        _chatServiceMock.Raise(x => x.OnReconnected += null);
        await Task.Delay(200);
        cut.Render();
        Assert.Contains("success", cut.Markup);
    }

    [Fact]
    [SuppressMessage("Major Code Smell", "S6966:Awaitable method should be used", Justification = "Raise doesn't support async")]
    public async Task Chat_OnSignalRDisconnected_ShouldNotAffectMuteNotification()
    {
        // Arrange
        SetupBasicAuth();
        SetupBasicMocks();
        var cut = Render<Chat>();
        await Task.Delay(200);

        // Act - Afficher une notification de mute
        _chatServiceMock.Raise(x => x.OnMessageBlocked += null, "Ce salon est muet");
        await Task.Delay(100);
        cut.Render();

        Assert.Contains("mute-notification", cut.Markup);
        Assert.Contains("Ce salon est muet", cut.Markup);

        // Act - Déclencher une déconnexion
        _chatServiceMock.Raise(x => x.OnDisconnected += null);
        await Task.Delay(200);
        cut.Render();

        // Assert - Les deux notifications devraient être visibles
        Assert.Contains("mute-notification", cut.Markup);
        Assert.Contains("Ce salon est muet", cut.Markup);
        Assert.Contains("connection-notification", cut.Markup);
        Assert.Contains("Connexion perdue", cut.Markup);
    }

    [Fact]
    public async Task Chat_InitializeSignalR_WhenFails_ShouldShowErrorNotification()
    {
        // Arrange
        SetupBasicAuth();
        _mockHttp.When(HttpMethod.Get, "*/api/channels")
            .Respond(HttpStatusCode.OK, request => JsonContent.Create(new List<Channel>()));

        _chatServiceMock.Setup(x => x.InitializeAsync(It.IsAny<IHubConnectionBuilder>()))
            .ThrowsAsync(new Exception("Connection failed"));

        _privateMessageServiceMock.Setup(x => x.GetConversationsAsync(It.IsAny<string>()))
            .ReturnsAsync([]);

        // Act
        var cut = Render<Chat>();
        await Task.Delay(300);
        cut.Render();

        // Assert - Devrait avoir isConnected = false
        Assert.Contains("Échec de connexion au serveur", cut.Markup);
    }

    private async Task<IRenderedComponent<Chat>> RenderChatAsync(
        string? channelName = null,
        string? privateUserId = null,
        string? privateUsername = null)
    {
        var cut = Render<Chat>();
        await Task.Delay(200);

        if (!string.IsNullOrEmpty(channelName) ||
            !string.IsNullOrEmpty(privateUserId) ||
            !string.IsNullOrEmpty(privateUsername))
        {
            await UpdateRouteAsync(cut, channelName, privateUserId, privateUsername);
        }

        return cut;
    }

    private static Task NavigateToChannelAsync(IRenderedComponent<Chat> cut, string channelName)
        => UpdateRouteAsync(cut, channelName, null, null);

    private static Task NavigateToPrivateChatAsync(
        IRenderedComponent<Chat> cut,
        string userId,
        string? username = null)
        => UpdateRouteAsync(cut, null, userId, username ?? userId);

    private static async Task UpdateRouteAsync(
        IRenderedComponent<Chat> cut,
        string? channelName,
        string? privateUserId,
        string? privateUsername)
    {
        var parameters = ParameterView.FromDictionary(new Dictionary<string, object?>
        {
            [nameof(Chat.ChannelName)] = channelName,
            [nameof(Chat.PrivateUserId)] = privateUserId,
            [nameof(Chat.PrivateUsername)] = privateUsername
        });

        await cut.InvokeAsync(() =>
            ((IComponent)cut.Instance).SetParametersAsync(parameters));
        await Task.Delay(100);
        cut.Render();
    }

    private void SetupBasicAuth(string username = "TestUser")
    {
        _authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);
        _authServiceMock.Setup(x => x.HasUsername).Returns(true);
        _authServiceMock.Setup(x => x.Username).Returns(username);
        _authServiceMock.Setup(x => x.GetClientUserIdAsync()).ReturnsAsync(username);
    }

    private void SetupBasicMocks()
    {
        _mockHttp.When(HttpMethod.Get, "*/api/channels")
            .Respond(HttpStatusCode.OK, request => JsonContent.Create(new List<Channel> { new() { Id = Guid.NewGuid(), Name = "general", CreatedBy = "system", CreatedAt = DateTime.UtcNow } }));

        _mockHttp.When(HttpMethod.Get, "*/api/my-channels?username=TestUser")
            .Respond(HttpStatusCode.OK, request => JsonContent.Create(new List<Channel> { new() { Id = Guid.NewGuid(), Name = "general", CreatedBy = "system", CreatedAt = DateTime.UtcNow } }));


        _chatServiceMock.Setup(x => x.InitializeAsync(It.IsAny<IHubConnectionBuilder>()))
            .Returns(Task.CompletedTask);
        _privateMessageServiceMock.Setup(x => x.GetConversationsAsync(It.IsAny<string>()))
            .ReturnsAsync([]);
    }
}