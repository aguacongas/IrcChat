// tests/IrcChat.Client.Tests/Components/ChannelMuteButtonTests.cs
using System.Net;
using System.Net.Http.Json;
using Bunit;
using FluentAssertions;
using IrcChat.Client.Components;
using Microsoft.Extensions.DependencyInjection;
using RichardSzalay.MockHttp;
using Xunit;

namespace IrcChat.Client.Tests.Components;

public class ChannelMuteButtonTests : TestContext
{
    private readonly MockHttpMessageHandler _mockHttp;

    public ChannelMuteButtonTests()
    {
        _mockHttp = new MockHttpMessageHandler();
        var httpClient = _mockHttp.ToHttpClient();
        httpClient.BaseAddress = new Uri("https://localhost:7000");
        Services.AddSingleton(httpClient);
    }

    [Fact]
    public void ChannelMuteButton_WhenCannotManage_ShouldNotShowButton()
    {
        // Act
        var cut = RenderComponent<ChannelMuteButton>(parameters => parameters
            .Add(p => p.ChannelName, "general")
            .Add(p => p.IsMuted, false)
            .Add(p => p.CanManage, false));

        // Assert
        cut.FindAll(".mute-btn").Should().BeEmpty();
    }

    [Fact]
    public void ChannelMuteButton_WhenCanManage_ShouldShowButton()
    {
        // Act
        var cut = RenderComponent<ChannelMuteButton>(parameters => parameters
            .Add(p => p.ChannelName, "general")
            .Add(p => p.IsMuted, false)
            .Add(p => p.CanManage, true));

        // Assert
        var button = cut.Find(".mute-btn");
        button.Should().NotBeNull();
        button.TextContent.Should().Contain("Muter le salon");
    }

    [Fact]
    public void ChannelMuteButton_WhenMuted_ShouldShowUnmuteButton()
    {
        // Act
        var cut = RenderComponent<ChannelMuteButton>(parameters => parameters
            .Add(p => p.ChannelName, "general")
            .Add(p => p.IsMuted, true)
            .Add(p => p.CanManage, true));

        // Assert
        var button = cut.Find(".mute-btn");
        button.TextContent.Should().Contain("Salon muet");
        button.ClassList.Should().Contain("muted");
    }

    [Fact]
    public void ChannelMuteButton_WhenCannotManageButMuted_ShouldShowIndicator()
    {
        // Act
        var cut = RenderComponent<ChannelMuteButton>(parameters => parameters
            .Add(p => p.ChannelName, "general")
            .Add(p => p.IsMuted, true)
            .Add(p => p.CanManage, false));

        // Assert
        cut.FindAll(".mute-btn").Should().BeEmpty();
        var indicator = cut.Find(".muted-indicator");
        indicator.Should().NotBeNull();
        indicator.TextContent.Should().Contain("Salon muet");
    }

    [Fact]
    public async Task ToggleMute_ShouldCallApiAndUpdateStatus()
    {
        // Arrange
        var muteToggleResponse = new { ChannelName = "general", IsMuted = true, ChangedBy = "admin" };

        _mockHttp.When(HttpMethod.Post, "*/api/channels/general/toggle-mute")
            .Respond(HttpStatusCode.OK, JsonContent.Create(muteToggleResponse));

        var statusChanged = false;
        var newStatus = false;

        var cut = RenderComponent<ChannelMuteButton>(parameters => parameters
            .Add(p => p.ChannelName, "general")
            .Add(p => p.IsMuted, false)
            .Add(p => p.CanManage, true)
            .Add(p => p.OnMuteStatusChanged, (bool isMuted) =>
            {
                statusChanged = true;
                newStatus = isMuted;
            }));

        // Act
        var button = cut.Find(".mute-btn");
        await cut.InvokeAsync(() => button.Click());
        await Task.Delay(200);

        // Assert
        statusChanged.Should().BeTrue();
        newStatus.Should().BeTrue();
    }

    [Fact]
    public async Task ToggleMute_OnError_ShouldShowErrorMessage()
    {
        // Arrange
        _mockHttp.When(HttpMethod.Post, "*/api/channels/general/toggle-mute")
            .Respond(HttpStatusCode.Forbidden);

        var cut = RenderComponent<ChannelMuteButton>(parameters => parameters
            .Add(p => p.ChannelName, "general")
            .Add(p => p.IsMuted, false)
            .Add(p => p.CanManage, true));

        // Act
        var button = cut.Find(".mute-btn");
        await cut.InvokeAsync(() => button.Click());
        await Task.Delay(200);
        cut.Render();

        // Assert
        cut.Markup.Should().Contain("Erreur");
    }

    [Fact]
    public async Task ToggleMute_WhileProcessing_ShouldDisableButton()
    {
        // Arrange
        _mockHttp.When(HttpMethod.Post, "*/api/channels/general/toggle-mute")
            .Respond(async () =>
            {
                await Task.Delay(1000);
                return new HttpResponseMessage(HttpStatusCode.OK);
            });

        var cut = RenderComponent<ChannelMuteButton>(parameters => parameters
            .Add(p => p.ChannelName, "general")
            .Add(p => p.IsMuted, false)
            .Add(p => p.CanManage, true));

        // Act
        var button = cut.Find(".mute-btn");
        await cut.InvokeAsync(() => button.Click());

        // Assert
        button.HasAttribute("disabled").Should().BeTrue();
    }

    [Fact]
    public async Task ToggleMute_WithEmptyChannelName_ShouldNotCallApi()
    {
        // Arrange
        var mockedRequest = _mockHttp.When(HttpMethod.Post, "*/api/channels/*/toggle-mute")
            .Respond(HttpStatusCode.OK);

        var cut = RenderComponent<ChannelMuteButton>(parameters => parameters
            .Add(p => p.ChannelName, "")
            .Add(p => p.IsMuted, false)
            .Add(p => p.CanManage, true));

        // Act
        var button = cut.Find(".mute-btn");
        await cut.InvokeAsync(() => button.Click());
        await Task.Delay(200);

        // Assert
        _mockHttp.GetMatchCount(mockedRequest).Should().Be(0);
    }

    [Fact]
    public async Task ToggleMute_ErrorMessageShouldDisappear()
    {
        // Arrange
        _mockHttp.When(HttpMethod.Post, "*/api/channels/general/toggle-mute")
            .Respond(HttpStatusCode.BadRequest);

        var cut = RenderComponent<ChannelMuteButton>(parameters => parameters
            .Add(p => p.ChannelName, "general")
            .Add(p => p.IsMuted, false)
            .Add(p => p.CanManage, true));

        // Act
        var button = cut.Find(".mute-btn");
        await cut.InvokeAsync(() => button.Click());
        await Task.Delay(200);
        cut.Render();

        cut.Markup.Should().Contain("Erreur");

        // Attendre que le message disparaisse
        await Task.Delay(3200);
        cut.Render();

        // Assert
        cut.Markup.Should().NotContain("Erreur");
    }

    [Fact]
    public async Task ToggleMute_ShouldNotTriggerWhenAlreadyProcessing()
    {
        // Arrange
        var request = _mockHttp.When(HttpMethod.Post, "*/api/channels/general/toggle-mute");
        request.Respond(async () =>
            {
                await Task.Delay(500);
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(new { ChannelName = "general", IsMuted = true, ChangedBy = "admin" })
                };
            });

        var cut = RenderComponent<ChannelMuteButton>(parameters => parameters
            .Add(p => p.ChannelName, "general")
            .Add(p => p.IsMuted, false)
            .Add(p => p.CanManage, true));

        var button = cut.Find(".mute-btn");

        // Act - Double click rapide
        await cut.InvokeAsync(() => button.Click());
        await cut.InvokeAsync(() => button.Click());
        await Task.Delay(600);
        cut.Render();

        // Assert - Devrait être appelé une seule fois
        var requestCount = _mockHttp.GetMatchCount(request);
        requestCount.Should().Be(1);
    }
}