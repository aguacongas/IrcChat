using System.Net;
using System.Net.Http.Json;
using Bunit;
using IrcChat.Shared.Models;
using Microsoft.AspNetCore.SignalR.Client;
using Moq;
using RichardSzalay.MockHttp;
using Xunit;

namespace IrcChat.Client.Tests.Pages;

public partial class ChatTests
{
    [Fact]
    public async Task Chat_HandleLeaveChannel_ShouldCallLeaveChannelService()
    {
        // Arrange
        SetupBasicAuth();
        var channels = new List<Channel>
        {
            new() { Id = Guid.NewGuid(), Name = "general", CreatedBy = "system", CreatedAt = DateTime.UtcNow, ConnectedUsersCount = 5 },
            new() { Id = Guid.NewGuid(), Name = "random", CreatedBy = "system", CreatedAt = DateTime.UtcNow, ConnectedUsersCount = 3 }
        };

        _mockHttp.When(HttpMethod.Get, "*/api/channels")
            .Respond(HttpStatusCode.OK, request => JsonContent.Create(channels));
        _mockHttp.When(HttpMethod.Get, "*/api/my-channels?username=TestUser")
            .Respond(HttpStatusCode.OK, request => JsonContent.Create(channels));
        _mockHttp.When(HttpMethod.Get, "*/api/messages/general*")
            .Respond(HttpStatusCode.OK, JsonContent.Create(new List<Message>()));
        _mockHttp.When(HttpMethod.Get, "*/api/channels/general/users")
            .Respond(HttpStatusCode.OK, JsonContent.Create(new List<User>()));

        _chatServiceMock.Setup(x => x.InitializeAsync(It.IsAny<IHubConnectionBuilder>()))
            .Returns(Task.CompletedTask);
        _chatServiceMock.Setup(x => x.JoinChannel("general")).Returns(Task.CompletedTask);
        _chatServiceMock.Setup(x => x.LeaveChannel("general")).Returns(Task.CompletedTask);
        _privateMessageServiceMock.Setup(x => x.GetConversationsAsync(It.IsAny<string>()))
            .ReturnsAsync([]);

        var cut = await RenderChatAsync(channelName: "general");

        // Act - Cliquer sur le bouton de fermeture du salon
        var leaveButton = await cut.InvokeAsync(() => cut.Find(".btn-leave-channel"));
        await cut.InvokeAsync(() => leaveButton.Click());
        await Task.Delay(200);

        // Assert
        _chatServiceMock.Verify(x => x.LeaveChannel("general"), Times.Once);
    }

    [Fact]
    public async Task Chat_LeaveCurrentChannel_ShouldSwitchToAnotherChannel()
    {
        // Arrange
        SetupBasicAuth();
        var channelsInitial = new List<Channel>
        {
            new() { Id = Guid.NewGuid(), Name = "general", CreatedBy = "system", CreatedAt = DateTime.UtcNow, ConnectedUsersCount = 5 },
            new() { Id = Guid.NewGuid(), Name = "random", CreatedBy = "system", CreatedAt = DateTime.UtcNow, ConnectedUsersCount = 3 }
        };

        var channelsAfterLeave = new List<Channel>
        {
            new() { Id = Guid.NewGuid(), Name = "random", CreatedBy = "system", CreatedAt = DateTime.UtcNow, ConnectedUsersCount = 3 }
        };

        _mockHttp.When(HttpMethod.Get, "*/api/channels")
            .Respond(HttpStatusCode.OK, request => JsonContent.Create(channelsInitial));
        _mockHttp.When(HttpMethod.Get, "*/api/my-channels?username=TestUser")
            .Respond(HttpStatusCode.OK, request => JsonContent.Create(channelsInitial));
        _mockHttp.When(HttpMethod.Get, "*/api/messages/general*")
            .Respond(HttpStatusCode.OK, JsonContent.Create(new List<Message>()));
        _mockHttp.When(HttpMethod.Get, "*/api/messages/random*")
            .Respond(HttpStatusCode.OK, JsonContent.Create(new List<Message>()));
        _mockHttp.When(HttpMethod.Get, "*/api/channels/general/users")
            .Respond(HttpStatusCode.OK, JsonContent.Create(new List<User>()));
        _mockHttp.When(HttpMethod.Get, "*/api/channels/random/users")
            .Respond(HttpStatusCode.OK, JsonContent.Create(new List<User>()));

        _chatServiceMock.Setup(x => x.InitializeAsync(It.IsAny<IHubConnectionBuilder>()))
            .Returns(Task.CompletedTask);
        _chatServiceMock.Setup(x => x.JoinChannel(It.IsAny<string>())).Returns(Task.CompletedTask);
        _chatServiceMock.Setup(x => x.LeaveChannel("general")).Returns(Task.CompletedTask);
        _privateMessageServiceMock.Setup(x => x.GetConversationsAsync(It.IsAny<string>()))
            .ReturnsAsync([]);

        var cut = await RenderChatAsync(channelName: "general");

        // Mettre à jour le mock pour retourner la liste après leave
        _mockHttp.ResetExpectations();
        _mockHttp.When(HttpMethod.Get, "*/api/my-channels?username=TestUser")
            .Respond(HttpStatusCode.OK, request => JsonContent.Create(channelsAfterLeave));

        // Act
        var leaveButton = cut.Find(".btn-leave-channel");
        await cut.InvokeAsync(() => leaveButton.Click());
        await Task.Delay(300);

        // Assert - Devrait naviguer vers random
        Assert.Contains("/chat/channel/random", _navManager.Uri);
    }

    [Fact]
    public async Task Chat_LeaveLastChannel_ShouldShowChannelList()
    {
        // Arrange
        SetupBasicAuth();
        var channels = new List<Channel>
        {
            new() { Id = Guid.NewGuid(), Name = "general", CreatedBy = "system", CreatedAt = DateTime.UtcNow, ConnectedUsersCount = 5 }
        };

        var emptyChannels = new List<Channel>();

        _mockHttp.When(HttpMethod.Get, "*/api/channels")
            .Respond(HttpStatusCode.OK, request => JsonContent.Create(channels));
        _mockHttp.When(HttpMethod.Get, "*/api/my-channels?username=TestUser")
            .Respond(HttpStatusCode.OK, request => JsonContent.Create(channels));
        _mockHttp.When(HttpMethod.Get, "*/api/messages/general*")
            .Respond(HttpStatusCode.OK, JsonContent.Create(new List<Message>()));
        _mockHttp.When(HttpMethod.Get, "*/api/channels/general/users")
            .Respond(HttpStatusCode.OK, JsonContent.Create(new List<User>()));

        _chatServiceMock.Setup(x => x.InitializeAsync(It.IsAny<IHubConnectionBuilder>()))
            .Returns(Task.CompletedTask);
        _chatServiceMock.Setup(x => x.JoinChannel("general")).Returns(Task.CompletedTask);
        _chatServiceMock.Setup(x => x.LeaveChannel("general")).Returns(Task.CompletedTask);
        _privateMessageServiceMock.Setup(x => x.GetConversationsAsync(It.IsAny<string>()))
            .ReturnsAsync([]);

        _navManager.NavigateTo("/chat/channel/general");
        var cut = await RenderChatAsync(channelName: "general");

        // Mettre à jour le mock pour retourner une liste vide après leave
        _mockHttp.ResetBackendDefinitions();
        _mockHttp.When(HttpMethod.Get, "*/api/my-channels?username=TestUser")
            .Respond(HttpStatusCode.OK, request => JsonContent.Create(emptyChannels));
        _mockHttp.When(HttpMethod.Get, "*/api/channels")
            .Respond(HttpStatusCode.OK, request => JsonContent.Create(new List<Channel> { channels[0] }));

        // Act
        var leaveButton = await cut.InvokeAsync(() => cut.Find(".btn-leave-channel"));
        await cut.InvokeAsync(() => leaveButton.Click());
        await Task.Delay(300);
        cut.Render();

        // Assert - Devrait naviguer vers /chat et afficher ChannelList
        Assert.EndsWith("/chat", _navManager.Uri);
        Assert.Contains("Aucun salon rejoint", cut.Markup);
    }

    [Fact]
    public async Task Chat_LeaveChannel_WhenNotCurrentChannel_ShouldStayOnCurrentChannel()
    {
        // Arrange
        SetupBasicAuth();
        var channels = new List<Channel>
        {
            new() { Id = Guid.NewGuid(), Name = "general", CreatedBy = "system", CreatedAt = DateTime.UtcNow, ConnectedUsersCount = 5 },
            new() { Id = Guid.NewGuid(), Name = "random", CreatedBy = "system", CreatedAt = DateTime.UtcNow, ConnectedUsersCount = 3 }
        };

        _mockHttp.When(HttpMethod.Get, "*/api/channels")
            .Respond(HttpStatusCode.OK, request => JsonContent.Create(channels));
        _mockHttp.When(HttpMethod.Get, "*/api/my-channels?username=TestUser")
            .Respond(HttpStatusCode.OK, request => JsonContent.Create(channels));
        _mockHttp.When(HttpMethod.Get, "*/api/messages/general*")
            .Respond(HttpStatusCode.OK, JsonContent.Create(new List<Message>()));
        _mockHttp.When(HttpMethod.Get, "*/api/channels/general/users")
            .Respond(HttpStatusCode.OK, JsonContent.Create(new List<User>()));

        _chatServiceMock.Setup(x => x.InitializeAsync(It.IsAny<IHubConnectionBuilder>()))
            .Returns(Task.CompletedTask);
        _chatServiceMock.Setup(x => x.JoinChannel("general")).Returns(Task.CompletedTask);
        _chatServiceMock.Setup(x => x.LeaveChannel("random")).Returns(Task.CompletedTask);
        _privateMessageServiceMock.Setup(x => x.GetConversationsAsync(It.IsAny<string>()))
            .ReturnsAsync([]);

        var cut = await RenderChatAsync(channelName: "general");
        var initialUri = _navManager.Uri;

        // Act - Quitter "random" alors qu'on est dans "general"
        var leaveButtons = cut.FindAll(".btn-leave-channel");
        var randomLeaveButton = leaveButtons[leaveButtons.Count - 1]; // Le deuxième bouton est pour "random"
        await cut.InvokeAsync(() => randomLeaveButton.Click());
        await Task.Delay(200);

        // Assert - Devrait rester sur general
        Assert.Equal(initialUri, _navManager.Uri);
    }

    [Fact]
    public async Task Chat_LeaveChannel_ShouldReloadMyChannels()
    {
        // Arrange
        SetupBasicAuth();
        var channels = new List<Channel>
        {
            new() { Id = Guid.NewGuid(), Name = "general", CreatedBy = "system", CreatedAt = DateTime.UtcNow, ConnectedUsersCount = 5 }
        };

        _mockHttp.When(HttpMethod.Get, "*/api/channels")
            .Respond(HttpStatusCode.OK, request => JsonContent.Create(channels));

        var myChannelsRequest = _mockHttp.When(HttpMethod.Get, "*/api/my-channels?username=TestUser")
            .Respond(HttpStatusCode.OK, request => JsonContent.Create(channels));

        _mockHttp.When(HttpMethod.Get, "*/api/messages/general*")
            .Respond(HttpStatusCode.OK, JsonContent.Create(new List<Message>()));
        _mockHttp.When(HttpMethod.Get, "*/api/channels/general/users")
            .Respond(HttpStatusCode.OK, JsonContent.Create(new List<User>()));

        _chatServiceMock.Setup(x => x.InitializeAsync(It.IsAny<IHubConnectionBuilder>()))
            .Returns(Task.CompletedTask);
        _chatServiceMock.Setup(x => x.JoinChannel("general")).Returns(Task.CompletedTask);
        _chatServiceMock.Setup(x => x.LeaveChannel("general")).Returns(Task.CompletedTask);
        _privateMessageServiceMock.Setup(x => x.GetConversationsAsync(It.IsAny<string>()))
            .ReturnsAsync([]);

        var cut = await RenderChatAsync(channelName: "general");

        // Act
        var leaveButton = cut.Find(".btn-leave-channel");
        await cut.InvokeAsync(() => leaveButton.Click());
        await Task.Delay(200);

        // Assert - Devrait avoir rechargé my-channels (initial + après leave)
        Assert.True(_mockHttp.GetMatchCount(myChannelsRequest) >= 2);
    }
}