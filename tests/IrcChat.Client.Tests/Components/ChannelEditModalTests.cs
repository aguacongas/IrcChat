// tests/IrcChat.Client.Tests/Components/ChannelEditModalTests.cs

using System.Net;
using System.Net.Http.Json;
using IrcChat.Client.Components;
using IrcChat.Shared.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using RichardSzalay.MockHttp;

namespace IrcChat.Client.Tests.Components;

public sealed class ChannelEditModalTests : BunitContext
{
    private readonly MockHttpMessageHandler mockHttp;

    public ChannelEditModalTests()
    {
        mockHttp = new MockHttpMessageHandler();
        var httpClient = mockHttp.ToHttpClient();
        httpClient.BaseAddress = new Uri("https://localhost:7000");
        Services.AddSingleton(httpClient);
        Services.AddLogging();
    }

    [Fact]
    public void ChannelEditModal_WhenRendered_ShouldDisplayChannelName()
    {
        // Arrange & Act
        var cut = Render<ChannelEditModal>(parameters => parameters
            .Add(p => p.ChannelName, "general")
            .Add(p => p.CurrentDescription, "Description actuelle"));

        // Assert
        Assert.Contains("#general", cut.Markup);
    }

    [Fact]
    public void ChannelEditModal_WhenRendered_ShouldDisplayCurrentDescription()
    {
        // Arrange
        var currentDescription = "Canal de discussion générale";

        // Act
        var cut = Render<ChannelEditModal>(parameters => parameters
            .Add(p => p.ChannelName, "general")
            .Add(p => p.CurrentDescription, currentDescription));

        var textarea = cut.Find("textarea");

        // Assert
        Assert.Equal(currentDescription, textarea.GetAttribute("value"));
    }

    [Fact]
    public void ChannelEditModal_WithNullDescription_ShouldDisplayEmptyTextarea()
    {
        // Arrange & Act
        var cut = Render<ChannelEditModal>(parameters => parameters
            .Add(p => p.ChannelName, "general")
            .Add(p => p.CurrentDescription, null));

        var textarea = cut.Find("textarea");

        // Assert
        Assert.Null(textarea.GetAttribute("value"));
    }

    [Fact]
    public void ChannelEditModal_CharCounter_ShouldDisplayCorrectCount()
    {
        // Arrange
        var description = "Test description";

        // Act
        var cut = Render<ChannelEditModal>(parameters => parameters
            .Add(p => p.ChannelName, "general")
            .Add(p => p.CurrentDescription, description));

        // Assert
        Assert.Contains($"{description.Length} / 200", cut.Markup);
    }

    [Fact]
    public async Task ChannelEditModal_CloseButton_ShouldInvokeOnClose()
    {
        // Arrange
        var onCloseCalled = false;

        var cut = Render<ChannelEditModal>(parameters => parameters
            .Add(p => p.ChannelName, "general")
            .Add(p => p.CurrentDescription, null)
            .Add(p => p.OnClose, EventCallback.Factory.Create(this, () => onCloseCalled = true)));

        // Act
        var closeButton = cut.Find(".close-button");
        await cut.InvokeAsync(() => closeButton.Click());

        // Assert
        Assert.True(onCloseCalled);
    }

    [Fact]
    public async Task ChannelEditModal_CancelButton_ShouldInvokeOnClose()
    {
        // Arrange
        var onCloseCalled = false;

        var cut = Render<ChannelEditModal>(parameters => parameters
            .Add(p => p.ChannelName, "general")
            .Add(p => p.CurrentDescription, null)
            .Add(p => p.OnClose, EventCallback.Factory.Create(this, () => onCloseCalled = true)));

        // Act
        var cancelButton = cut.Find(".btn-secondary");
        await cut.InvokeAsync(() => cancelButton.Click());

        // Assert
        Assert.True(onCloseCalled);
    }

    [Fact]
    public async Task ChannelEditModal_SaveChanges_WithValidData_ShouldCallApi()
    {
        // Arrange
        var channelName = "general";
        var newDescription = "Nouvelle description";

        var putRequest = mockHttp
            .When(HttpMethod.Put, $"*/api/channels/{Uri.EscapeDataString(channelName)}")
            .Respond(HttpStatusCode.OK, JsonContent.Create(new { message = "Success" }));

        var cut = Render<ChannelEditModal>(parameters => parameters
            .Add(p => p.ChannelName, channelName)
            .Add(p => p.CurrentDescription, null));

        // Act
        var textarea = cut.Find("textarea");
        await cut.InvokeAsync(() => textarea.Input(newDescription));

        var saveButton = cut.Find(".btn-primary");
        await cut.InvokeAsync(() => saveButton.Click());

        await Task.Delay(100);

        // Assert
        var count = mockHttp.GetMatchCount(putRequest);
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task ChannelEditModal_SaveChanges_ShouldSendTrimmedDescription()
    {
        // Arrange
        var channelName = "general";
        var descriptionWithSpaces = "   Description avec espaces   ";
        Channel? sentChannel = null;

        mockHttp
            .When(HttpMethod.Put, $"*/api/channels/{Uri.EscapeDataString(channelName)}")
            .Respond(async request =>
            {
                sentChannel = await request.Content!.ReadFromJsonAsync<Channel>();
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(new { message = "Success" }),
                };
            });

        var cut = Render<ChannelEditModal>(parameters => parameters
            .Add(p => p.ChannelName, channelName)
            .Add(p => p.CurrentDescription, null));

        // Act
        var textarea = cut.Find("textarea");
        await cut.InvokeAsync(() => textarea.Input(descriptionWithSpaces));

        var saveButton = cut.Find(".btn-primary");
        await cut.InvokeAsync(() => saveButton.Click());

        await Task.Delay(100);

        // Assert
        Assert.NotNull(sentChannel);
        Assert.Equal("Description avec espaces", sentChannel.Description);
    }

    [Fact]
    public async Task ChannelEditModal_SaveChanges_WithEmptyDescription_ShouldSendNull()
    {
        // Arrange
        var channelName = "general";
        Channel? sentChannel = null;

        mockHttp
            .When(HttpMethod.Put, $"*/api/channels/{Uri.EscapeDataString(channelName)}")
            .Respond(async request =>
            {
                sentChannel = await request.Content!.ReadFromJsonAsync<Channel>();
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(new { message = "Success" }),
                };
            });

        var cut = Render<ChannelEditModal>(parameters => parameters
            .Add(p => p.ChannelName, channelName)
            .Add(p => p.CurrentDescription, "Old description"));

        // Act
        var textarea = cut.Find("textarea");
        await cut.InvokeAsync(() => textarea.Input("   "));

        var saveButton = cut.Find(".btn-primary");
        await cut.InvokeAsync(() => saveButton.Click());

        await Task.Delay(100);

        // Assert
        Assert.NotNull(sentChannel);
        Assert.Null(sentChannel.Description);
    }

    [Fact]
    public async Task ChannelEditModal_SaveChanges_OnSuccess_ShouldShowSuccessMessage()
    {
        // Arrange
        mockHttp
            .When(HttpMethod.Put, "*/api/channels/*")
            .Respond(HttpStatusCode.OK, JsonContent.Create(new { message = "Success" }));

        var cut = Render<ChannelEditModal>(parameters => parameters
            .Add(p => p.ChannelName, "general")
            .Add(p => p.CurrentDescription, null));

        var textarea = cut.Find("textarea");
        await cut.InvokeAsync(() => textarea.Input("Nouvelle description"));

        // Act
        var saveButton = cut.Find(".btn-primary");
        await cut.InvokeAsync(() => saveButton.Click());

        await cut.WaitForStateAsync(() => cut.Markup.Contains("mise à jour avec succès"), TimeSpan.FromSeconds(2));

        // Assert
        Assert.Contains("mise à jour avec succès", cut.Markup);
        Assert.Contains("alert-success", cut.Markup);
    }

    [Fact]
    public async Task ChannelEditModal_SaveChanges_OnSuccess_ShouldInvokeOnSaved()
    {
        // Arrange
        var onSavedCalled = false;

        mockHttp
            .When(HttpMethod.Put, "*/api/channels/*")
            .Respond(HttpStatusCode.OK, JsonContent.Create(new { message = "Success" }));

        var cut = Render<ChannelEditModal>(parameters => parameters
            .Add(p => p.ChannelName, "general")
            .Add(p => p.CurrentDescription, null)
            .Add(p => p.OnSaved, EventCallback.Factory.Create(this, () => onSavedCalled = true)));

        var textarea = cut.Find("textarea");
        await cut.InvokeAsync(() => textarea.Input("Nouvelle description"));

        // Act
        var saveButton = cut.Find(".btn-primary");
        await cut.InvokeAsync(() => saveButton.Click());

        await Task.Delay(1500);

        // Assert
        Assert.True(onSavedCalled);
    }

    [Fact]
    public async Task ChannelEditModal_SaveChanges_OnNotFound_ShouldShowError()
    {
        // Arrange
        mockHttp
            .When(HttpMethod.Put, "*/api/channels/*")
            .Respond(HttpStatusCode.NotFound, JsonContent.Create(new { error = "channel_not_found" }));

        var cut = Render<ChannelEditModal>(parameters => parameters
            .Add(p => p.ChannelName, "general")
            .Add(p => p.CurrentDescription, null));

        var textarea = cut.Find("textarea");
        await cut.InvokeAsync(() => textarea.Input("Nouvelle description"));

        // Act
        var saveButton = cut.Find(".btn-primary");
        await cut.InvokeAsync(() => saveButton.Click());

        await cut.WaitForStateAsync(() => cut.Markup.Contains("n'existe plus"), TimeSpan.FromSeconds(2));

        // Assert
        Assert.Contains("n'existe plus", cut.Markup);
        Assert.Contains("alert-error", cut.Markup);
    }

    [Theory]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.Unauthorized)]
    public async Task ChannelEditModal_SaveChanges_OnForbidden_ShouldShowPermissionError(HttpStatusCode statusCode)
    {
        // Arrange
        mockHttp
            .When(HttpMethod.Put, "*/api/channels/*")
            .Respond(statusCode);

        var cut = Render<ChannelEditModal>(parameters => parameters
            .Add(p => p.ChannelName, "general")
            .Add(p => p.CurrentDescription, null));

        var textarea = cut.Find("textarea");
        await cut.InvokeAsync(() => textarea.Input("Nouvelle description"));

        // Act
        var saveButton = cut.Find(".btn-primary");
        await cut.InvokeAsync(() => saveButton.Click());
        cut.Render();

        await cut.WaitForStateAsync(() => cut.Markup.Contains("permission"), TimeSpan.FromSeconds(2));

        // Assert
        Assert.Contains("permission", cut.Markup);
        Assert.Contains("alert-error", cut.Markup);
    }

    [Fact]
    public async Task ChannelEditModal_SaveChanges_OnError_ShouldShowGenericError()
    {
        // Arrange
        mockHttp
            .When(HttpMethod.Put, "*/api/channels/*")
            .Respond(HttpStatusCode.InternalServerError);

        var cut = Render<ChannelEditModal>(parameters => parameters
            .Add(p => p.ChannelName, "general")
            .Add(p => p.CurrentDescription, null));

        var textarea = cut.Find("textarea");
        await cut.InvokeAsync(() => textarea.Input("Nouvelle description"));

        // Act
        var saveButton = cut.Find(".btn-primary");
        await cut.InvokeAsync(() => saveButton.Click());

        await cut.WaitForStateAsync(() => cut.Markup.Contains("Erreur"), TimeSpan.FromSeconds(2));

        // Assert
        Assert.Contains("Erreur", cut.Markup);
        Assert.Contains("alert-error", cut.Markup);
    }

    [Fact]
    public async Task ChannelEditModal_WhileSaving_ShouldDisableButton()
    {
        // Arrange
        mockHttp
            .When(HttpMethod.Put, "*/api/channels/*")
            .Respond(async () =>
            {
                await Task.Delay(500);
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(new { message = "Success" }),
                };
            });

        var cut = Render<ChannelEditModal>(parameters => parameters
            .Add(p => p.ChannelName, "general")
            .Add(p => p.CurrentDescription, null));

        var textarea = cut.Find("textarea");
        await cut.InvokeAsync(() => textarea.Input("Nouvelle description"));

        var saveButton = cut.Find(".btn-primary");

        // Act
        await cut.InvokeAsync(() => saveButton.Click());

        // Assert - Le bouton doit être désactivé pendant la sauvegarde
        await cut.WaitForStateAsync(
            () =>
        {
            var button = cut.Find(".btn-primary");
            return button.HasAttribute("disabled");
        },
            TimeSpan.FromSeconds(1));

        var disabledButton = cut.Find(".btn-primary");
        Assert.True(disabledButton.HasAttribute("disabled"));
    }

    [Fact]
    public async Task ChannelEditModal_WhileSaving_ShouldShowSpinner()
    {
        // Arrange
        mockHttp
            .When(HttpMethod.Put, "*/api/channels/*")
            .Respond(async () =>
            {
                await Task.Delay(500);
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(new { message = "Success" }),
                };
            });

        var cut = Render<ChannelEditModal>(parameters => parameters
            .Add(p => p.ChannelName, "general")
            .Add(p => p.CurrentDescription, null));

        var textarea = cut.Find("textarea");
        await cut.InvokeAsync(() => textarea.Input("Nouvelle description"));

        var saveButton = cut.Find(".btn-primary");

        // Act
        await cut.InvokeAsync(() => saveButton.Click());

        // Assert
        await cut.WaitForStateAsync(() => cut.Markup.Contains("spinner-small"), TimeSpan.FromSeconds(1));
        Assert.Contains("spinner-small", cut.Markup);
    }

    [Fact]
    public async Task ChannelEditModal_ClickingOverlay_ShouldInvokeOnClose()
    {
        // Arrange
        var onCloseCalled = false;

        var cut = Render<ChannelEditModal>(parameters => parameters
            .Add(p => p.ChannelName, "general")
            .Add(p => p.CurrentDescription, null)
            .Add(p => p.OnClose, EventCallback.Factory.Create(this, () => onCloseCalled = true)));

        // Act
        var overlay = cut.Find(".modal-overlay");
        await cut.InvokeAsync(() => overlay.Click());

        // Assert
        Assert.True(onCloseCalled);
    }
}