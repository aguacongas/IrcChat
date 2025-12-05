// tests/IrcChat.Client.Tests/Components/GlobalMuteUserButtonTests.cs
using System.Net;
using System.Net.Http.Json;
using IrcChat.Client.Components;
using IrcChat.Client.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RichardSzalay.MockHttp;

namespace IrcChat.Client.Tests.Components;

public class GlobalMuteUserButtonTests : BunitContext
{
    private readonly MockHttpMessageHandler mockHttp;
    private readonly Mock<IUnifiedAuthService> authServiceMock;
    private readonly Mock<ILogger<GlobalMuteUserButton>> loggerMock;

    public GlobalMuteUserButtonTests()
    {
        mockHttp = new MockHttpMessageHandler();
        authServiceMock = new Mock<IUnifiedAuthService>();
        loggerMock = new Mock<ILogger<GlobalMuteUserButton>>();

        var httpClient = mockHttp.ToHttpClient();
        httpClient.BaseAddress = new Uri("https://localhost:7000");
        Services.AddSingleton(httpClient);
        Services.AddSingleton(authServiceMock.Object);
        Services.AddSingleton(loggerMock.Object);

        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public void GlobalMuteUserButton_WhenNotAdmin_ShouldNotRender()
    {
        // Arrange
        authServiceMock.Setup(x => x.IsAdmin).Returns(false);

        // Act
        var cut = Render<GlobalMuteUserButton>(parameters => parameters
            .Add(p => p.UserId, "user123"));

        // Assert
        Assert.Empty(cut.Markup);
    }

    [Fact]
    public void GlobalMuteUserButton_WhenAdminAndNoUserId_ShouldNotRender()
    {
        // Arrange
        authServiceMock.Setup(x => x.IsAdmin).Returns(true);

        // Act
        var cut = Render<GlobalMuteUserButton>(parameters => parameters
            .Add(p => p.UserId, string.Empty));

        // Assert
        Assert.Empty(cut.Markup);
    }

    [Fact]
    public async Task GlobalMuteUserButton_WhenAdminAndUserNotMuted_ShouldShowMuteButton()
    {
        // Arrange
        authServiceMock.Setup(x => x.IsAdmin).Returns(true);
        authServiceMock.Setup(x => x.Token).Returns("fake-token");

        mockHttp.When(HttpMethod.Get, "*/api/admin/global-mute/user123/is-muted")
            .Respond("application/json", "{\"userId\":\"user123\",\"isGloballyMuted\":false}");

        // Act
        var cut = Render<GlobalMuteUserButton>(parameters => parameters
            .Add(p => p.UserId, "user123"));

        await Task.Delay(100); // Attendre l'initialisation

        // Assert
        Assert.Contains("global-mute-btn", cut.Markup);
        Assert.Contains("mute", cut.Markup);
        Assert.Contains("🔇", cut.Markup);
        Assert.Contains("Mute", cut.Markup);
    }

    [Fact]
    public async Task GlobalMuteUserButton_WhenAdminAndUserMuted_ShouldShowUnmuteButton()
    {
        // Arrange
        authServiceMock.Setup(x => x.IsAdmin).Returns(true);
        authServiceMock.Setup(x => x.Token).Returns("fake-token");

        mockHttp.When(HttpMethod.Get, "*/api/admin/global-mute/user123/is-muted")
            .Respond("application/json", "{\"userId\":\"user123\",\"isGloballyMuted\":true}");

        // Act
        var cut = Render<GlobalMuteUserButton>(parameters => parameters
            .Add(p => p.UserId, "user123"));

        await Task.Delay(100);

        // Assert
        Assert.Contains("global-mute-btn", cut.Markup);
        Assert.Contains("unmute", cut.Markup);
        Assert.Contains("🔊", cut.Markup);
        Assert.Contains("Unmute", cut.Markup);
    }

    [Fact]
    public async Task GlobalMuteUserButton_WhenClickedOnMuteButton_ShouldShowMuteDialog()
    {
        // Arrange
        authServiceMock.Setup(x => x.IsAdmin).Returns(true);
        authServiceMock.Setup(x => x.Token).Returns("fake-token");

        mockHttp.When(HttpMethod.Get, "*/api/admin/global-mute/user123/is-muted")
            .Respond("application/json", "{\"userId\":\"user123\",\"isGloballyMuted\":false}");

        var cut = Render<GlobalMuteUserButton>(parameters => parameters
            .Add(p => p.UserId, "user123"));

        await Task.Delay(100);

        // Act
        var button = cut.Find(".global-mute-btn");
        await cut.InvokeAsync(() => button.Click());

        // Assert
        Assert.Contains("dialog-overlay", cut.Markup);
        Assert.Contains("Muter l'utilisateur globalement", cut.Markup);
        Assert.Contains("mute-reason-input", cut.Markup);
    }

    [Fact]
    public async Task GlobalMuteUserButton_WhenClickedOnUnmuteButton_ShouldShowUnmuteDialog()
    {
        // Arrange
        authServiceMock.Setup(x => x.IsAdmin).Returns(true);
        authServiceMock.Setup(x => x.Token).Returns("fake-token");

        mockHttp.When(HttpMethod.Get, "*/api/admin/global-mute/user123/is-muted")
            .Respond("application/json", "{\"userId\":\"user123\",\"isGloballyMuted\":true}");

        var cut = Render<GlobalMuteUserButton>(parameters => parameters
            .Add(p => p.UserId, "user123"));

        await Task.Delay(100);

        // Act
        var button = cut.Find(".global-mute-btn");
        await cut.InvokeAsync(() => button.Click());

        // Assert
        Assert.Contains("dialog-overlay", cut.Markup);
        Assert.Contains("Unmuter l'utilisateur", cut.Markup);
        Assert.Contains("Êtes-vous sûr", cut.Markup);
    }

    [Fact]
    public async Task GlobalMuteUserButton_WhenCancelDialog_ShouldCloseDialog()
    {
        // Arrange
        authServiceMock.Setup(x => x.IsAdmin).Returns(true);
        authServiceMock.Setup(x => x.Token).Returns("fake-token");

        mockHttp.When(HttpMethod.Get, "*/api/admin/global-mute/user123/is-muted")
            .Respond("application/json", "{\"userId\":\"user123\",\"isGloballyMuted\":false}");

        var cut = Render<GlobalMuteUserButton>(parameters => parameters
            .Add(p => p.UserId, "user123"));

        await Task.Delay(100);

        var button = cut.Find(".global-mute-btn");
        await cut.InvokeAsync(() => button.Click());

        // Act
        var cancelButton = cut.Find(".cancel-btn");
        await cut.InvokeAsync(() => cancelButton.Click());

        // Assert
        Assert.DoesNotContain("dialog-overlay", cut.Markup);
    }

    [Fact]
    public async Task GlobalMuteUserButton_WhenClickOverlay_ShouldCloseDialog()
    {
        // Arrange
        authServiceMock.Setup(x => x.IsAdmin).Returns(true);
        authServiceMock.Setup(x => x.Token).Returns("fake-token");

        mockHttp.When(HttpMethod.Get, "*/api/admin/global-mute/user123/is-muted")
            .Respond("application/json", "{\"userId\":\"user123\",\"isGloballyMuted\":false}");

        var cut = Render<GlobalMuteUserButton>(parameters => parameters
            .Add(p => p.UserId, "user123"));

        await Task.Delay(100);

        var button = cut.Find(".global-mute-btn");
        await cut.InvokeAsync(() => button.Click());

        // Act
        var overlay = cut.Find(".dialog-overlay");
        await cut.InvokeAsync(() => overlay.Click());

        // Assert
        Assert.DoesNotContain("dialog-overlay", cut.Markup);
    }

    [Fact]
    public async Task GlobalMuteUserButton_WhenClickCloseButton_ShouldCloseDialog()
    {
        // Arrange
        authServiceMock.Setup(x => x.IsAdmin).Returns(true);
        authServiceMock.Setup(x => x.Token).Returns("fake-token");

        mockHttp.When(HttpMethod.Get, "*/api/admin/global-mute/user123/is-muted")
            .Respond("application/json", "{\"userId\":\"user123\",\"isGloballyMuted\":false}");

        var cut = Render<GlobalMuteUserButton>(parameters => parameters
            .Add(p => p.UserId, "user123"));

        await Task.Delay(100);

        var button = cut.Find(".global-mute-btn");
        await cut.InvokeAsync(() => button.Click());

        // Act
        var closeButton = cut.Find(".dialog-close");
        await cut.InvokeAsync(() => closeButton.Click());

        // Assert
        Assert.DoesNotContain("dialog-overlay", cut.Markup);
    }

    [Fact]
    public async Task GlobalMuteUserButton_WhenConfirmMute_ShouldCallApiAndShowSuccess()
    {
        // Arrange
        authServiceMock.Setup(x => x.IsAdmin).Returns(true);
        authServiceMock.Setup(x => x.Token).Returns("fake-token");

        mockHttp.When(HttpMethod.Get, "*/api/admin/global-mute/user123/is-muted")
            .Respond("application/json", "{\"userId\":\"user123\",\"isGloballyMuted\":false}");

        mockHttp.When(HttpMethod.Post, "*/api/admin/global-mute/user123")
            .Respond(HttpStatusCode.OK, "application/json", "{\"userId\":\"user123\"}");

        var cut = Render<GlobalMuteUserButton>(parameters => parameters
            .Add(p => p.UserId, "user123"));

        await Task.Delay(100);

        var button = cut.Find(".global-mute-btn");
        await cut.InvokeAsync(() => button.Click());

        // Act
        var confirmButton = cut.Find(".confirm-btn");
        await cut.InvokeAsync(() => confirmButton.Click());
        await Task.Delay(100);

        // Assert
        Assert.DoesNotContain("dialog-overlay", cut.Markup);
        Assert.Contains("success-tooltip", cut.Markup);
        Assert.Contains("Utilisateur muté globalement", cut.Markup);
    }

    [Fact]
    public async Task GlobalMuteUserButton_WhenConfirmMuteWithReason_ShouldSendReasonToApi()
    {
        // Arrange
        authServiceMock.Setup(x => x.IsAdmin).Returns(true);
        authServiceMock.Setup(x => x.Token).Returns("fake-token");

        mockHttp.When(HttpMethod.Get, "*/api/admin/global-mute/user123/is-muted")
            .Respond("application/json", "{\"userId\":\"user123\",\"isGloballyMuted\":false}");

        var capturedRequest = (HttpRequestMessage?)null;
        mockHttp.When(HttpMethod.Post, "*/api/admin/global-mute/user123")
            .Respond(async request =>
            {
                capturedRequest = request;
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(new { userId = "user123" }),
                };
            });

        var cut = Render<GlobalMuteUserButton>(parameters => parameters
            .Add(p => p.UserId, "user123"));

        await Task.Delay(100);

        var button = await cut.InvokeAsync(() => cut.Find(".global-mute-btn"));
        await cut.InvokeAsync(() => button.Click());

        var textarea = await cut.InvokeAsync(() => cut.Find(".mute-reason-input"));
        await cut.InvokeAsync(() => textarea.Change("Test reason"));
        cut.Render();

        // Act
        var confirmButton = await cut.InvokeAsync(() => cut.Find(".confirm-btn"));
        await cut.InvokeAsync(() => confirmButton.Click());
        await Task.Delay(100);

        // Assert
        Assert.NotNull(capturedRequest);
        var content = await capturedRequest!.Content!.ReadAsStringAsync();
        Assert.Contains("Test reason", content);
    }

    [Fact]
    public async Task GlobalMuteUserButton_WhenConfirmUnmute_ShouldCallApiAndShowSuccess()
    {
        // Arrange
        authServiceMock.Setup(x => x.IsAdmin).Returns(true);
        authServiceMock.Setup(x => x.Token).Returns("fake-token");

        mockHttp.When(HttpMethod.Get, "*/api/admin/global-mute/user123/is-muted")
            .Respond("application/json", "{\"userId\":\"user123\",\"isGloballyMuted\":true}");

        mockHttp.When(HttpMethod.Delete, "*/api/admin/global-mute/user123")
            .Respond(HttpStatusCode.OK);

        var cut = Render<GlobalMuteUserButton>(parameters => parameters
            .Add(p => p.UserId, "user123"));

        await Task.Delay(100);

        var button = cut.Find(".global-mute-btn");
        await cut.InvokeAsync(() => button.Click());

        // Act
        var confirmButton = cut.Find(".confirm-btn");
        await cut.InvokeAsync(() => confirmButton.Click());
        await Task.Delay(100);

        // Assert
        Assert.DoesNotContain("dialog-overlay", cut.Markup);
        Assert.Contains("success-tooltip", cut.Markup);
        Assert.Contains("Utilisateur réactivé", cut.Markup);
    }

    [Fact]
    public async Task GlobalMuteUserButton_WhenMuteApiFails_ShouldShowError()
    {
        // Arrange
        authServiceMock.Setup(x => x.IsAdmin).Returns(true);
        authServiceMock.Setup(x => x.Token).Returns("fake-token");

        mockHttp.When(HttpMethod.Get, "*/api/admin/global-mute/user123/is-muted")
            .Respond("application/json", "{\"userId\":\"user123\",\"isGloballyMuted\":false}");

        mockHttp.When(HttpMethod.Post, "*/api/admin/global-mute/user123")
            .Respond(HttpStatusCode.BadRequest, "application/json", "{\"error\":\"user_already_globally_muted\"}");

        var cut = Render<GlobalMuteUserButton>(parameters => parameters
            .Add(p => p.UserId, "user123"));

        await Task.Delay(100);

        var button = cut.Find(".global-mute-btn");
        await cut.InvokeAsync(() => button.Click());

        // Act
        var confirmButton = cut.Find(".confirm-btn");
        await cut.InvokeAsync(() => confirmButton.Click());
        await Task.Delay(100);

        // Assert
        Assert.Contains("error-tooltip", cut.Markup);
        Assert.Contains("Erreur lors du mute", cut.Markup);
    }

    [Fact]
    public async Task GlobalMuteUserButton_WhenUnmuteApiFails_ShouldShowError()
    {
        // Arrange
        authServiceMock.Setup(x => x.IsAdmin).Returns(true);
        authServiceMock.Setup(x => x.Token).Returns("fake-token");

        mockHttp.When(HttpMethod.Get, "*/api/admin/global-mute/user123/is-muted")
            .Respond("application/json", "{\"userId\":\"user123\",\"isGloballyMuted\":true}");

        mockHttp.When(HttpMethod.Delete, "*/api/admin/global-mute/user123")
            .Respond(HttpStatusCode.NotFound);

        var cut = Render<GlobalMuteUserButton>(parameters => parameters
            .Add(p => p.UserId, "user123"));

        await Task.Delay(100);

        var button = cut.Find(".global-mute-btn");
        await cut.InvokeAsync(() => button.Click());

        // Act
        var confirmButton = cut.Find(".confirm-btn");
        await cut.InvokeAsync(() => confirmButton.Click());
        await Task.Delay(100);

        // Assert
        Assert.Contains("error-tooltip", cut.Markup);
        Assert.Contains("Erreur lors du unmute", cut.Markup);
    }

    [Fact]
    public async Task GlobalMuteUserButton_WhenLoading_ShouldShowSpinner()
    {
        // Arrange
        authServiceMock.Setup(x => x.IsAdmin).Returns(true);
        authServiceMock.Setup(x => x.Token).Returns("fake-token");

        mockHttp.When(HttpMethod.Get, "*/api/admin/global-mute/user123/is-muted")
            .Respond("application/json", "{\"userId\":\"user123\",\"isGloballyMuted\":false}");

        var tcs = new TaskCompletionSource<HttpResponseMessage>();
        mockHttp.When(HttpMethod.Post, "*/api/admin/global-mute/user123")
            .Respond(async () => await tcs.Task);

        var cut = Render<GlobalMuteUserButton>(parameters => parameters
            .Add(p => p.UserId, "user123"));

        await Task.Delay(100);

        var button = cut.Find(".global-mute-btn");
        await cut.InvokeAsync(() => button.Click());

        var confirmButton = cut.Find(".confirm-btn");
        await cut.InvokeAsync(() => confirmButton.Click());

        // Act & Assert - pendant le chargement
        await Task.Delay(50);
        Assert.Contains("spinner-icon", cut.Markup);
        Assert.Contains("disabled", cut.Markup);

        // Terminer la requête
        tcs.SetResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new { userId = "user123" }),
        });
    }

    [Fact]
    public async Task GlobalMuteUserButton_WhenCheckStatusFails_ShouldLogError()
    {
        // Arrange
        authServiceMock.Setup(x => x.IsAdmin).Returns(true);
        authServiceMock.Setup(x => x.Token).Returns("fake-token");

        mockHttp.When(HttpMethod.Get, "*/api/admin/global-mute/user123/is-muted")
            .Respond(HttpStatusCode.InternalServerError);

        // Act
        Render<GlobalMuteUserButton>(parameters => parameters
            .Add(p => p.UserId, "user123"));

        await Task.Delay(100);

        // Assert
        loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Erreur lors de la vérification du statut")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task GlobalMuteUserButton_WhenMuteThrowsException_ShouldShowErrorAndLog()
    {
        // Arrange
        authServiceMock.Setup(x => x.IsAdmin).Returns(true);
        authServiceMock.Setup(x => x.Token).Returns("fake-token");

        mockHttp.When(HttpMethod.Get, "*/api/admin/global-mute/user123/is-muted")
            .Respond("application/json", "{\"userId\":\"user123\",\"isGloballyMuted\":false}");

        mockHttp.When(HttpMethod.Post, "*/api/admin/global-mute/user123")
            .Throw(new HttpRequestException("Network error"));

        var cut = Render<GlobalMuteUserButton>(parameters => parameters
            .Add(p => p.UserId, "user123"));

        await Task.Delay(100);

        var button = cut.Find(".global-mute-btn");
        await cut.InvokeAsync(() => button.Click());

        // Act
        var confirmButton = cut.Find(".confirm-btn");
        await cut.InvokeAsync(() => confirmButton.Click());
        await Task.Delay(100);

        // Assert
        Assert.Contains("error-tooltip", cut.Markup);
        Assert.Contains("Une erreur est survenue", cut.Markup);

        loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Erreur lors du mute global")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task GlobalMuteUserButton_WhenUserIdChanges_ShouldRecheckStatus()
    {
        // Arrange
        authServiceMock.Setup(x => x.IsAdmin).Returns(true);
        authServiceMock.Setup(x => x.Token).Returns("fake-token");

        mockHttp.When(HttpMethod.Get, "*/api/admin/global-mute/user123/is-muted")
            .Respond("application/json", "{\"userId\":\"user123\",\"isGloballyMuted\":false}");

        mockHttp.When(HttpMethod.Get, "*/api/admin/global-mute/user456/is-muted")
            .Respond("application/json", "{\"userId\":\"user456\",\"isGloballyMuted\":true}");

        var cut = Render<GlobalMuteUserButton>(parameters => parameters
            .Add(p => p.UserId, "user123"));

        await Task.Delay(100);
        Assert.Contains("🔇", cut.Markup); // Not muted

        // Act - Changer le UserId
        cut.Render(parameters => parameters
            .Add(p => p.UserId, "user456"));

        await Task.Delay(100);

        // Assert
        Assert.Contains("🔊", cut.Markup); // Muted
    }

    [Fact]
    public async Task GlobalMuteUserButton_MuteDialogTextarea_ShouldShowCharCount()
    {
        // Arrange
        authServiceMock.Setup(x => x.IsAdmin).Returns(true);
        authServiceMock.Setup(x => x.Token).Returns("fake-token");

        mockHttp.When(HttpMethod.Get, "*/api/admin/global-mute/user123/is-muted")
            .Respond("application/json", "{\"userId\":\"user123\",\"isGloballyMuted\":false}");

        var cut = Render<GlobalMuteUserButton>(parameters => parameters
            .Add(p => p.UserId, "user123"));

        await Task.Delay(100);

        var button = await cut.InvokeAsync(() => cut.Find(".global-mute-btn"));
        await cut.InvokeAsync(() => button.Click());

        // Act
        var textarea = await cut.InvokeAsync(() => cut.Find(".mute-reason-input"));
        await cut.InvokeAsync(() => textarea.Change("Test"));

        // Assert
        Assert.Contains("char-count", cut.Markup);
        Assert.Contains("4 / 500", cut.Markup);
    }

    [Fact]
    public async Task GlobalMuteUserButton_WhenEmptyReason_ShouldUseDefaultReason()
    {
        // Arrange
        authServiceMock.Setup(x => x.IsAdmin).Returns(true);
        authServiceMock.Setup(x => x.Token).Returns("fake-token");

        mockHttp.When(HttpMethod.Get, "*/api/admin/global-mute/user123/is-muted")
            .Respond("application/json", "{\"userId\":\"user123\",\"isGloballyMuted\":false}");

        var capturedRequest = (HttpRequestMessage?)null;
        mockHttp.When(HttpMethod.Post, "*/api/admin/global-mute/user123")
            .Respond(async request =>
            {
                capturedRequest = request;
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(new { userId = "user123" }),
                };
            });

        var cut = Render<GlobalMuteUserButton>(parameters => parameters
            .Add(p => p.UserId, "user123"));

        await Task.Delay(100);

        var button = await cut.InvokeAsync(() => cut.Find(".global-mute-btn"));
        await cut.InvokeAsync(() => button.Click());

        // Act - Ne pas entrer de raison
        var confirmButton = await cut.InvokeAsync(() => cut.Find(".confirm-btn"));
        await cut.InvokeAsync(() => confirmButton.Click());
        await Task.Delay(100);

        // Assert
        Assert.NotNull(capturedRequest);
        var content = await capturedRequest!.Content!.ReadAsStringAsync();
        Assert.Contains("Mut\\u00E9 globalement par un administrateur", content);
    }
}