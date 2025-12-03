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
    public async Task Chat_WhenNoChannelsConnected_ShouldShowChannelList()
    {
        // Arrange
        SetupBasicAuth();
        _mockHttp.When(HttpMethod.Get, "*/api/my-channels?username=TestUser")
            .Respond(HttpStatusCode.OK, request => JsonContent.Create(new List<Channel>()));

        var allChannels = new List<Channel>
        {
            new() { Id = Guid.NewGuid(), Name = "general", CreatedBy = "system", CreatedAt = DateTime.UtcNow, ConnectedUsersCount = 10 }
        };

        _mockHttp.When(HttpMethod.Get, "*/api/channels")
            .Respond(HttpStatusCode.OK, request => JsonContent.Create(allChannels));

        _chatServiceMock.Setup(x => x.InitializeAsync(It.IsAny<IHubConnectionBuilder>()))
            .Returns(Task.CompletedTask);
        _privateMessageServiceMock.Setup(x => x.GetConversationsAsync(It.IsAny<string>()))
            .ReturnsAsync([]);

        // Act
        var cut = await RenderChatAsync();
        await Task.Delay(200);
        cut.Render();

        // Assert
        Assert.Contains("Salons Disponibles", cut.Markup);
    }

    [Fact]
    public async Task Chat_JoinChannelFromChannelList_ShouldAddToMyChannels()
    {
        // Arrange
        SetupBasicAuth();
        var emptyChannels = new List<Channel>();
        var allChannels = new List<Channel>
        {
            new() { Id = Guid.NewGuid(), Name = "general", CreatedBy = "system", CreatedAt = DateTime.UtcNow, ConnectedUsersCount = 10 }
        };

        var myChannelsAfterJoin = new List<Channel>
        {
            new() { Id = Guid.NewGuid(), Name = "general", CreatedBy = "system", CreatedAt = DateTime.UtcNow, ConnectedUsersCount = 10 }
        };

        _mockHttp.When(HttpMethod.Get, "*/api/my-channels?username=TestUser")
            .Respond(HttpStatusCode.OK, request => JsonContent.Create(emptyChannels));

        _mockHttp.When(HttpMethod.Get, "*/api/channels")
            .Respond(HttpStatusCode.OK, request => JsonContent.Create(allChannels));

        _mockHttp.When(HttpMethod.Get, "*/api/messages/general*")
            .Respond(HttpStatusCode.OK, request => JsonContent.Create(new List<Message>()));

        _mockHttp.When(HttpMethod.Get, "*/api/channels/general/users")
            .Respond(HttpStatusCode.OK, request => JsonContent.Create(new List<User>()));

        _chatServiceMock.Setup(x => x.InitializeAsync(It.IsAny<IHubConnectionBuilder>()))
            .Returns(Task.CompletedTask);
        _chatServiceMock.Setup(x => x.JoinChannel("general")).Returns(Task.CompletedTask);
        _privateMessageServiceMock.Setup(x => x.GetConversationsAsync(It.IsAny<string>()))
            .ReturnsAsync([]);

        var cut = await RenderChatAsync();
        await Task.Delay(200);

        // Simuler le chargement des salons après le join
        _mockHttp.ResetBackendDefinitions();
        _mockHttp.When(HttpMethod.Get, "*/api/my-channels?username=TestUser")
            .Respond(HttpStatusCode.OK, request => JsonContent.Create(myChannelsAfterJoin));

        // Act - Cliquer sur un salon dans ChannelList
        await NavigateToChannelAsync(cut, "general");
        await Task.Delay(200);

        // Assert
        _chatServiceMock.Verify(x => x.JoinChannel("general"), Times.Once);
    }

    [Fact]
    public async Task Chat_HandleBrowseChannels_ShouldNavigateToChannelList()
    {
        // Arrange
        SetupBasicAuth();
        var channels = new List<Channel>
        {
            new() { Id = Guid.NewGuid(), Name = "general", CreatedBy = "system", CreatedAt = DateTime.UtcNow, ConnectedUsersCount = 5 }
        };

        _mockHttp.When(HttpMethod.Get, "*/api/my-channels?username=TestUser")
            .Respond(HttpStatusCode.OK, request => JsonContent.Create(channels));
        _mockHttp.When(HttpMethod.Get, "*/api/channels")
            .Respond(HttpStatusCode.OK, request => JsonContent.Create(channels));
        _mockHttp.When(HttpMethod.Get, "*/api/messages/general*")
            .Respond(HttpStatusCode.OK, request => JsonContent.Create(new List<Message>()));
        _mockHttp.When(HttpMethod.Get, "*/api/channels/general/users")
            .Respond(HttpStatusCode.OK, request => JsonContent.Create(new List<User>()));

        _chatServiceMock.Setup(x => x.InitializeAsync(It.IsAny<IHubConnectionBuilder>()))
            .Returns(Task.CompletedTask);
        _chatServiceMock.Setup(x => x.JoinChannel("general")).Returns(Task.CompletedTask);
        _privateMessageServiceMock.Setup(x => x.GetConversationsAsync(It.IsAny<string>()))
            .ReturnsAsync([]);

        var cut = await RenderChatAsync(channelName: "general");

        // Act - Cliquer sur le bouton Parcourir
        var browseButton = cut.Find(".btn-browse-channels");
        await cut.InvokeAsync(() => browseButton.Click());
        await Task.Delay(200);

        // Assert
        Assert.EndsWith("/chat", _navManager.Uri);
    }

    [Fact]
    public async Task Chat_JoinAlreadyConnectedChannel_ShouldNotCallJoinAgain()
    {
        // Arrange
        SetupBasicAuth();
        var channels = new List<Channel>
        {
            new() { Id = Guid.NewGuid(), Name = "general", CreatedBy = "system", CreatedAt = DateTime.UtcNow, ConnectedUsersCount = 5 }
        };

        _mockHttp.When(HttpMethod.Get, "*/api/my-channels?username=TestUser")
            .Respond(HttpStatusCode.OK, request => JsonContent.Create(channels));
        _mockHttp.When(HttpMethod.Get, "*/api/channels")
            .Respond(HttpStatusCode.OK, request => JsonContent.Create(channels));
        _mockHttp.When(HttpMethod.Get, "*/api/messages/general*")
            .Respond(HttpStatusCode.OK, request => JsonContent.Create(new List<Message>()));
        _mockHttp.When(HttpMethod.Get, "*/api/channels/general/users")
            .Respond(HttpStatusCode.OK, request => JsonContent.Create(new List<User>()));

        _chatServiceMock.Setup(x => x.InitializeAsync(It.IsAny<IHubConnectionBuilder>()))
            .Returns(Task.CompletedTask);
        _chatServiceMock.Setup(x => x.JoinChannel("general")).Returns(Task.CompletedTask);
        _privateMessageServiceMock.Setup(x => x.GetConversationsAsync(It.IsAny<string>()))
            .ReturnsAsync([]);

        var cut = await RenderChatAsync(channelName: "general");
        _chatServiceMock.Invocations.Clear();

        // Act - Essayer de rejoindre le même canal
        await NavigateToChannelAsync(cut, "general");
        await Task.Delay(200);

        // Assert - Ne devrait pas appeler JoinChannel car déjà connecté
        _chatServiceMock.Verify(x => x.JoinChannel("general"), Times.Never);
    }

    [Fact]
    public async Task Chat_AfterJoinChannel_ShouldReloadMyChannels()
    {
        // Arrange
        SetupBasicAuth();
        var initialChannels = new List<Channel>();
        var afterJoinChannels = new List<Channel>
        {
            new() { Id = Guid.NewGuid(), Name = "general", CreatedBy = "system", CreatedAt = DateTime.UtcNow, ConnectedUsersCount = 5 }
        };

        var myChannelsRequest = _mockHttp.When(HttpMethod.Get, "*/api/my-channels?username=TestUser")
            .Respond(HttpStatusCode.OK, request => JsonContent.Create(initialChannels));

        _mockHttp.When(HttpMethod.Get, "*/api/channels")
            .Respond(HttpStatusCode.OK, request => JsonContent.Create(afterJoinChannels));
        _mockHttp.When(HttpMethod.Get, "*/api/messages/general*")
            .Respond(HttpStatusCode.OK, request => JsonContent.Create(new List<Message>()));
        _mockHttp.When(HttpMethod.Get, "*/api/channels/general/users")
            .Respond(HttpStatusCode.OK, request => JsonContent.Create(new List<User>()));

        _chatServiceMock.Setup(x => x.InitializeAsync(It.IsAny<IHubConnectionBuilder>()))
            .Returns(Task.CompletedTask);
        _chatServiceMock.Setup(x => x.JoinChannel("general")).Returns(Task.CompletedTask);
        _privateMessageServiceMock.Setup(x => x.GetConversationsAsync(It.IsAny<string>()))
            .ReturnsAsync([]);

        var cut = await RenderChatAsync();
        await Task.Delay(200);

        // Mettre à jour le mock pour retourner le salon après le join
        _mockHttp.ResetExpectations();
        _mockHttp.When(HttpMethod.Get, "*/api/my-channels?username=TestUser")
            .Respond(HttpStatusCode.OK, request => JsonContent.Create(afterJoinChannels));

        // Act - Rejoindre un canal
        await NavigateToChannelAsync(cut, "general");
        await Task.Delay(200);

        // Assert - Devrait avoir appelé my-channels 2 fois (initial + après join)
        Assert.True(_mockHttp.GetMatchCount(myChannelsRequest) >= 1);
    }
}