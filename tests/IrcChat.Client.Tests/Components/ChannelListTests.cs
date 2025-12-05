using System.Net;
using System.Net.Http.Json;
using IrcChat.Client.Components;
using IrcChat.Shared.Models;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using RichardSzalay.MockHttp;

namespace IrcChat.Client.Tests.Components;

public class ChannelListTests : BunitContext
{
    private readonly MockHttpMessageHandler mockHttp;

    public ChannelListTests()
    {
        mockHttp = new MockHttpMessageHandler();
        var httpClient = mockHttp.ToHttpClient();
        httpClient.BaseAddress = new Uri("https://localhost:7000");
        Services.AddSingleton(httpClient);
    }

    [Fact]
    public async Task ChannelList_OnInitialized_ShouldLoadChannels()
    {
        // Arrange
        var channels = new List<Channel>
        {
            new() { Id = Guid.NewGuid(), Name = "general", Description = "General discussion", CreatedBy = "system", CreatedAt = DateTime.UtcNow, ConnectedUsersCount = 5 },
            new() { Id = Guid.NewGuid(), Name = "random", Description = "Random stuff", CreatedBy = "system", CreatedAt = DateTime.UtcNow, ConnectedUsersCount = 3 },
        };

        mockHttp.When(HttpMethod.Get, "*/api/channels")
            .Respond(HttpStatusCode.OK, JsonContent.Create(channels));

        // Act
        var cut = Render<ChannelList>();
        await Task.Delay(200);
        cut.Render();

        // Assert
        Assert.Contains("general", cut.Markup);
        Assert.Contains("random", cut.Markup);
        Assert.Contains("General discussion", cut.Markup);
        Assert.Contains("Random stuff", cut.Markup);
    }

    [Fact]
    public async Task ChannelList_ShouldDisplayChannelUserCounts()
    {
        // Arrange
        var channels = new List<Channel>
        {
            new() { Id = Guid.NewGuid(), Name = "general", CreatedBy = "system", CreatedAt = DateTime.UtcNow, ConnectedUsersCount = 10 },
            new() { Id = Guid.NewGuid(), Name = "random", CreatedBy = "system", CreatedAt = DateTime.UtcNow, ConnectedUsersCount = 5 },
        };

        mockHttp.When(HttpMethod.Get, "*/api/channels")
            .Respond(HttpStatusCode.OK, JsonContent.Create(channels));

        // Act
        var cut = Render<ChannelList>();
        await Task.Delay(200);
        cut.Render();

        // Assert
        Assert.Contains("10", cut.Markup);
        Assert.Contains("5", cut.Markup);
    }

    [Fact]
    public async Task ChannelList_WithSearch_ShouldFilterChannels()
    {
        // Arrange
        var channels = new List<Channel>
        {
            new() { Id = Guid.NewGuid(), Name = "general", Description = "General discussion", CreatedBy = "system", CreatedAt = DateTime.UtcNow, ConnectedUsersCount = 5 },
            new() { Id = Guid.NewGuid(), Name = "random", Description = "Random stuff", CreatedBy = "system", CreatedAt = DateTime.UtcNow, ConnectedUsersCount = 3 },
            new() { Id = Guid.NewGuid(), Name = "tech", Description = "Technology", CreatedBy = "admin", CreatedAt = DateTime.UtcNow, ConnectedUsersCount = 8 },
        };

        mockHttp.When(HttpMethod.Get, "*/api/channels")
            .Respond(HttpStatusCode.OK, JsonContent.Create(channels));

        var cut = Render<ChannelList>();
        await Task.Delay(200);

        // Act - Rechercher "tech"
        var searchInput = await cut.InvokeAsync(() => cut.Find(".search-input"));
        await cut.InvokeAsync(() => searchInput.Input("tech"));
        await cut.InvokeAsync(() => searchInput.KeyUp());
        await Task.Delay(100);
        cut.Render();

        // Assert
        Assert.Contains("tech", cut.Markup);
        Assert.DoesNotContain("general", cut.Markup);
        Assert.DoesNotContain("random", cut.Markup);
    }

    [Fact]
    public async Task ChannelList_WithSearchByDescription_ShouldFilterChannels()
    {
        // Arrange
        var channels = new List<Channel>
        {
            new() { Id = Guid.NewGuid(), Name = "general", Description = "General discussion", CreatedBy = "system", CreatedAt = DateTime.UtcNow, ConnectedUsersCount = 5 },
            new() { Id = Guid.NewGuid(), Name = "tech", Description = "Technology talks", CreatedBy = "admin", CreatedAt = DateTime.UtcNow, ConnectedUsersCount = 8 },
        };

        mockHttp.When(HttpMethod.Get, "*/api/channels")
            .Respond(HttpStatusCode.OK, JsonContent.Create(channels));

        var cut = Render<ChannelList>();
        await Task.Delay(200);

        // Act - Rechercher "technology"
        var searchInput = await cut.InvokeAsync(() => cut.Find(".search-input"));
        await cut.InvokeAsync(() => searchInput.Input("technology"));
        await cut.InvokeAsync(() => searchInput.KeyUp());
        await Task.Delay(100);
        cut.Render();

        // Assert
        Assert.Contains("tech", cut.Markup);
        Assert.DoesNotContain("general", cut.Markup);
    }

    [Fact]
    public async Task ChannelList_OnChannelClick_ShouldInvokeCallback()
    {
        // Arrange
        var channels = new List<Channel>
        {
            new() { Id = Guid.NewGuid(), Name = "general", CreatedBy = "system", CreatedAt = DateTime.UtcNow, ConnectedUsersCount = 5 },
        };

        mockHttp.When(HttpMethod.Get, "*/api/channels")
            .Respond(HttpStatusCode.OK, JsonContent.Create(channels));

        var selectedChannel = string.Empty;
        var cut = Render<ChannelList>(parameters => parameters
            .Add(p => p.OnChannelSelected, (channelName) =>
            {
                selectedChannel = channelName;
                return Task.CompletedTask;
            }));

        await Task.Delay(200);

        // Act
        var channelCard = await cut.InvokeAsync(() => cut.Find(".channel-card"));
        await cut.InvokeAsync(() => channelCard.Click());
        await Task.Delay(100);

        // Assert
        Assert.Equal("general", selectedChannel);
    }

    [Fact]
    public async Task ChannelList_WhenEmpty_ShouldShowEmptyState()
    {
        // Arrange
        mockHttp.When(HttpMethod.Get, "*/api/channels")
            .Respond(HttpStatusCode.OK, JsonContent.Create(new List<Channel>()));

        // Act
        var cut = Render<ChannelList>();
        await Task.Delay(200);
        cut.Render();

        // Assert
        Assert.Contains("Aucun salon disponible", cut.Markup);
    }

    [Fact]
    public async Task ChannelList_WhenSearchNoResults_ShouldShowNoResultsMessage()
    {
        // Arrange
        var channels = new List<Channel>
        {
            new() { Id = Guid.NewGuid(), Name = "general", CreatedBy = "system", CreatedAt = DateTime.UtcNow, ConnectedUsersCount = 5 },
        };

        mockHttp.When(HttpMethod.Get, "*/api/channels")
            .Respond(HttpStatusCode.OK, JsonContent.Create(channels));

        var cut = Render<ChannelList>();
        await Task.Delay(200);

        // Act
        var searchInput = await cut.InvokeAsync(() => cut.Find(".search-input"));
        await cut.InvokeAsync(() => searchInput.Input("nonexistent"));
        await cut.InvokeAsync(() => searchInput.KeyUp());
        await Task.Delay(100);
        cut.Render();

        // Assert
        Assert.Contains("Aucun résultat pour", cut.Markup);
        Assert.Contains("nonexistent", cut.Markup);
    }

    [Fact]
    public async Task ChannelList_OnLoadError_ShouldShowErrorMessage()
    {
        // Arrange
        mockHttp.When(HttpMethod.Get, "*/api/channels")
            .Respond(HttpStatusCode.InternalServerError);

        // Act
        var cut = Render<ChannelList>();
        await Task.Delay(200);
        cut.Render();

        // Assert
        Assert.Contains("Impossible de charger les salons", cut.Markup);
    }

    [Fact]
    public async Task ChannelList_OnRetryClick_ShouldReloadChannels()
    {
        // Arrange
        var channelsRequest = mockHttp.When(HttpMethod.Get, "*/api/channels")
            .Respond(HttpStatusCode.InternalServerError);

        var cut = Render<ChannelList>();
        await Task.Delay(200);

        // Act - Cliquer sur réessayer
        var retryButton = await cut.InvokeAsync(() => cut.Find(".btn-retry"));
        await cut.InvokeAsync(() => retryButton.Click());
        await Task.Delay(200);

        // Assert - Devrait avoir tenté 2 fois (initial + retry)
        Assert.Equal(2, mockHttp.GetMatchCount(channelsRequest));
    }

    [Fact]
    public async Task ChannelList_ClearSearch_ShouldShowAllChannels()
    {
        // Arrange
        var channels = new List<Channel>
        {
            new() { Id = Guid.NewGuid(), Name = "general", CreatedBy = "system", CreatedAt = DateTime.UtcNow, ConnectedUsersCount = 5 },
            new() { Id = Guid.NewGuid(), Name = "random", CreatedBy = "system", CreatedAt = DateTime.UtcNow, ConnectedUsersCount = 3 },
        };

        mockHttp.When(HttpMethod.Get, "*/api/channels")
            .Respond(HttpStatusCode.OK, JsonContent.Create(channels));

        var cut = Render<ChannelList>();
        await Task.Delay(200);

        // Filtrer
        var searchInput = await cut.InvokeAsync(() => cut.Find(".search-input"));
        await cut.InvokeAsync(() => searchInput.Input("nonexistent"));
        await cut.InvokeAsync(() => searchInput.KeyUp());
        await Task.Delay(100);
        cut.Render();

        Assert.Contains("Aucun résultat", cut.Markup);

        // Act - Effacer la recherche
        var clearButton = await cut.InvokeAsync(() => cut.Find(".btn-clear-search"));
        await cut.InvokeAsync(() => clearButton.Click());
        await Task.Delay(100);
        cut.Render();

        // Assert
        Assert.Contains("general", cut.Markup);
        Assert.Contains("random", cut.Markup);
    }

    [Fact]
    public async Task ChannelList_ShouldDisplayMutedBadge()
    {
        // Arrange
        var channels = new List<Channel>
        {
            new() { Id = Guid.NewGuid(), Name = "general", CreatedBy = "system", CreatedAt = DateTime.UtcNow, IsMuted = true, ConnectedUsersCount = 5 },
        };

        mockHttp.When(HttpMethod.Get, "*/api/channels")
            .Respond(HttpStatusCode.OK, JsonContent.Create(channels));

        // Act
        var cut = Render<ChannelList>();
        await Task.Delay(200);
        cut.Render();

        // Assert
        Assert.Contains("Muet", cut.Markup);
    }
}