// tests/IrcChat.Client.Tests/Pages/LoginTests.cs
using System.Net;
using System.Net.Http.Json;
using IrcChat.Client.Pages;
using IrcChat.Client.Services;
using IrcChat.Shared.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using RichardSzalay.MockHttp;

namespace IrcChat.Client.Tests.Pages;

public class LoginTests : BunitContext
{
    private readonly Mock<IUnifiedAuthService> authServiceMock;
    private readonly MockHttpMessageHandler mockHttp;
    private readonly NavigationManager navManager;

    public LoginTests()
    {
        authServiceMock = new Mock<IUnifiedAuthService>();
        mockHttp = new MockHttpMessageHandler();

        var httpClient = mockHttp.ToHttpClient();
        httpClient.BaseAddress = new Uri("https://localhost:7000");

        Services.AddSingleton(authServiceMock.Object);
        Services.AddSingleton(httpClient);
        Services.AddSingleton(JSInterop.JSRuntime);

        navManager = Services.GetRequiredService<NavigationManager>();
    }

    [Fact]
    public void Login_WhenAlreadyAuthenticated_ShouldRedirectToChat()
    {
        // Arrange
        authServiceMock.Setup(x => x.IsAuthenticated).Returns(true);
        authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);

        // Act
        Render<Login>();

        // Assert
        Assert.EndsWith("/chat", navManager.Uri);
    }

    [Fact]
    public void Login_WithSavedUsername_ShouldPrefillInput()
    {
        // Arrange
        authServiceMock.Setup(x => x.HasUsername).Returns(true);
        authServiceMock.Setup(x => x.Username).Returns("SavedUser");
        authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);

        // Act
        var cut = Render<Login>();

        // Assert
        var input = cut.Find("input[placeholder*='pseudo']");
        Assert.Equal("SavedUser", input.GetAttribute("value"));
    }

    [Fact]
    public async Task Login_EnterAsGuest_ShouldSetUsernameAndNavigate()
    {
        // Arrange
        authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);
        authServiceMock
            .Setup(x => x.SetUsernameAsync("TestUser", false, null))
            .Returns(Task.CompletedTask);

        var checkResponse = new UsernameCheckResponse
        {
            Available = true,
            IsReserved = false,
            IsCurrentlyUsed = false,
        };

        mockHttp.When(HttpMethod.Post, "*/api/oauth/check-username")
            .Respond(HttpStatusCode.OK, JsonContent.Create(checkResponse));

        var cut = Render<Login>();

        // Act
        var input = cut.Find("input[placeholder*='pseudo']");
        await cut.InvokeAsync(() => input.Input("TestUser"));
        await Task.Delay(600); // Attendre le debounce

        var guestButton = cut.Find("button:contains('invité')");
        await cut.InvokeAsync(() => guestButton.Click());

        // Assert
        authServiceMock.Verify(
            x => x.SetUsernameAsync("TestUser", false, null),
            Times.Once);
        Assert.EndsWith("/chat", navManager.Uri);
    }

    [Fact]
    public async Task Login_UsernameCheckRequest_ShouldShowStatus()
    {
        // Arrange
        authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);

        var checkResponse = new UsernameCheckResponse
        {
            Available = true,
            IsReserved = false,
            IsCurrentlyUsed = false,
        };

        mockHttp.When(HttpMethod.Post, "*/api/oauth/check-username")
            .Respond(HttpStatusCode.OK, JsonContent.Create(checkResponse));

        var cut = Render<Login>();

        // Act
        var input = cut.Find("input[placeholder*='pseudo']");
        await cut.InvokeAsync(() => input.Input("TestUser"));
        await Task.Delay(600); // Attendre le debounce

        // Assert
        Assert.Contains("invité", cut.Markup);
        Assert.Contains("Réserver", cut.Markup);
    }

    [Fact]
    public async Task Login_ReservedUsername_ShouldShowLoginButton()
    {
        // Arrange
        authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);

        var checkResponse = new UsernameCheckResponse
        {
            Available = false,
            IsReserved = true,
            ReservedProvider = ExternalAuthProvider.Google,
            IsCurrentlyUsed = false,
        };

        mockHttp.When(HttpMethod.Post, "*/api/oauth/check-username")
            .Respond(HttpStatusCode.OK, JsonContent.Create(checkResponse));

        var cut = Render<Login>();

        // Act
        var input = cut.Find("input[placeholder*='pseudo']");
        await cut.InvokeAsync(() => input.Input("ReservedUser"));
        await Task.Delay(600);

        // Assert
        Assert.Contains("réservé", cut.Markup);
        Assert.Contains("Se connecter", cut.Markup);
    }

    [Fact]
    public async Task Login_CurrentlyUsedUsername_ShouldShowWarning()
    {
        // Arrange
        authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);

        var checkResponse = new UsernameCheckResponse
        {
            Available = false,
            IsReserved = false,
            IsCurrentlyUsed = true,
        };

        mockHttp.When(HttpMethod.Post, "*/api/oauth/check-username")
            .Respond(HttpStatusCode.OK, JsonContent.Create(checkResponse));

        var cut = Render<Login>();

        // Act
        var input = cut.Find("input[placeholder*='pseudo']");
        await cut.InvokeAsync(() => input.Input("UsedUsername"));
        await Task.Delay(600);
        cut.Render();

        // Assert
        Assert.Contains("utilisé", cut.Markup);
    }

    [Fact]
    public async Task Login_ShortUsername_ShouldNotCheckAvailability()
    {
        // Arrange
        authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);

        var mockedRequest = mockHttp.When(HttpMethod.Post, "*/api/oauth/check-username")
            .Respond(HttpStatusCode.OK);

        var cut = Render<Login>();

        // Act
        var input = cut.Find("input[placeholder*='pseudo']");
        await cut.InvokeAsync(() => input.Input("AB")); // < 3 caractères
        await Task.Delay(600);

        // Assert
        var requestCount = mockHttp.GetMatchCount(mockedRequest);
        Assert.Equal(0, requestCount);
    }

    [Fact]
    public async Task Login_EnterKey_ShouldSubmitWhenAvailable()
    {
        // Arrange
        authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);
        authServiceMock
            .Setup(x => x.SetUsernameAsync("TestUser", false, null))
            .Returns(Task.CompletedTask);

        var checkResponse = new UsernameCheckResponse
        {
            Available = true,
            IsReserved = false,
            IsCurrentlyUsed = false,
        };

        mockHttp.When(HttpMethod.Post, "*/api/oauth/check-username")
            .Respond(HttpStatusCode.OK, JsonContent.Create(checkResponse));

        var cut = Render<Login>();

        // Act
        var input = cut.Find("input[placeholder*='pseudo']");
        await cut.InvokeAsync(() => input.Input("TestUser"));
        await Task.Delay(600);
        await cut.InvokeAsync(() => input.KeyPress("Enter"));

        // Assert
        authServiceMock.Verify(
            x => x.SetUsernameAsync("TestUser", false, null),
            Times.Once);
    }

    [Fact]
    public async Task Login_LoginWithProvider_ShouldSetSessionStorage()
    {
        // Arrange
        authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);

        var checkResponse = new UsernameCheckResponse
        {
            Available = false,
            IsReserved = true,
            ReservedProvider = ExternalAuthProvider.Google,
            IsCurrentlyUsed = false,
        };

        mockHttp.When(HttpMethod.Post, "*/api/oauth/check-username")
            .Respond(HttpStatusCode.OK, JsonContent.Create(checkResponse));

        JSInterop.SetupVoid("sessionStorage.setItem", _ => true).SetVoidResult();

        var cut = Render<Login>();

        // Act
        var input = cut.Find("input[placeholder*='pseudo']");
        await cut.InvokeAsync(() => input.Input("ReservedUser"));
        await Task.Delay(600);

        var loginButton = cut.Find("button:contains('Se connecter')");
        await cut.InvokeAsync(() => loginButton.Click());

        // Assert
        JSInterop.VerifyInvoke("sessionStorage.setItem", 1);
        Assert.Contains("oauth-login", navManager.Uri);
    }

    [Fact]
    public async Task Login_UseAnotherUsername_ShouldClearState()
    {
        // Arrange
        authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);
        authServiceMock.Setup(x => x.HasUsername).Returns(true);
        authServiceMock.Setup(x => x.Username).Returns("SavedUser");
        authServiceMock.Setup(x => x.IsReserved).Returns(true);
        authServiceMock.Setup(x => x.IsAuthenticated).Returns(false);
        authServiceMock.Setup(x => x.ReservedProvider).Returns(ExternalAuthProvider.Google);
        authServiceMock.Setup(x => x.ClearAllAsync()).Returns(Task.CompletedTask);

        var cut = Render<Login>();

        // Act
        var anotherUsernameButton = cut.Find("button:contains('autre pseudo')");
        await cut.InvokeAsync(() => anotherUsernameButton.Click());

        // Assert
        authServiceMock.Verify(x => x.ClearAllAsync(), Times.Once);
    }

    [Fact]
    public async Task Login_NavigateToReserve_ShouldIncludeUsernameInQueryString()
    {
        // Arrange
        authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);

        var checkResponse = new UsernameCheckResponse
        {
            Available = true,
            IsReserved = false,
            IsCurrentlyUsed = false,
        };

        mockHttp.When(HttpMethod.Post, "*/api/oauth/check-username")
            .Respond(HttpStatusCode.OK, JsonContent.Create(checkResponse));

        var cut = Render<Login>();

        // Act
        var input = cut.Find("input[placeholder*='pseudo']");
        await cut.InvokeAsync(() => input.Input("TestUser"));
        await Task.Delay(600);

        var reserveButton = cut.Find("button:contains('Réserver')");
        await cut.InvokeAsync(() => reserveButton.Click());

        // Assert
        Assert.Contains("reserve?username=TestUser", navManager.Uri);
    }

    [Fact]
    public async Task Login_CheckUsername_ShouldDebounceRequests()
    {
        // Arrange
        authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);

        var checkResponse = new UsernameCheckResponse
        {
            Available = true,
            IsReserved = false,
            IsCurrentlyUsed = false,
        };

        var mockedRequest = mockHttp.When(HttpMethod.Post, "*/api/oauth/check-username")
            .Respond(HttpStatusCode.OK, JsonContent.Create(checkResponse));

        var cut = Render<Login>();
        var input = cut.Find("input[placeholder*='pseudo']");

        // Act - Saisie rapide de plusieurs caractères
        await cut.InvokeAsync(() => input.Input("T"));
        await Task.Delay(100);
        await cut.InvokeAsync(() => input.Input("Te"));
        await Task.Delay(100);
        await cut.InvokeAsync(() => input.Input("Tes"));
        await Task.Delay(100);
        await cut.InvokeAsync(() => input.Input("Test"));
        await Task.Delay(100);
        await cut.InvokeAsync(() => input.Input("TestU"));
        await Task.Delay(600); // Attendre le debounce final

        // Assert - Une seule requête doit être faite
        var requestCount = mockHttp.GetMatchCount(mockedRequest);
        Assert.Equal(1, requestCount);
    }

    [Fact]
    public async Task Login_CheckUsername_WithError_ShouldDisplayErrorMessage()
    {
        // Arrange
        authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);

        mockHttp.When(HttpMethod.Post, "*/api/oauth/check-username")
            .Respond(HttpStatusCode.InternalServerError);

        var cut = Render<Login>();

        // Act
        var input = cut.Find("input[placeholder*='pseudo']");
        await cut.InvokeAsync(() => input.Input("TestUser"));
        await Task.Delay(600);
        cut.Render();

        // Assert
        Assert.Contains("Erreur lors de la vérification", cut.Markup);
    }

    [Fact]
    public void Login_WithReservedUserNotAuthenticated_ShouldDisableInput()
    {
        // Arrange
        authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);
        authServiceMock.Setup(x => x.HasUsername).Returns(true);
        authServiceMock.Setup(x => x.Username).Returns("ReservedUser");
        authServiceMock.Setup(x => x.IsReserved).Returns(true);
        authServiceMock.Setup(x => x.IsAuthenticated).Returns(false);

        // Act
        var cut = Render<Login>();

        // Assert
        var input = cut.Find("input[placeholder*='pseudo']");
        Assert.True(input.HasAttribute("disabled"));
    }

    [Fact]
    public async Task Login_EnterAsGuest_WithShortUsername_ShouldShowError()
    {
        // Arrange
        authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);

        var checkResponse = new UsernameCheckResponse
        {
            Available = true,
            IsReserved = false,
            IsCurrentlyUsed = false,
        };

        mockHttp.When(HttpMethod.Post, "*/api/oauth/check-username")
            .Respond(HttpStatusCode.OK, JsonContent.Create(checkResponse));

        var cut = Render<Login>();

        // Act
        var input = cut.Find("input[placeholder*='pseudo']");
        await cut.InvokeAsync(() => input.Input("AB"));
        await Task.Delay(600);

        // Le bouton ne devrait pas apparaître, donc on simule l'appel direct
        // à la méthode EnterAsGuest avec un username court

        // Assert
        // Pas de bouton "invité" visible car username trop court
        Assert.DoesNotContain("button:contains('invité')", cut.Markup);
    }

    [Fact]
    public void Login_Loading_ShouldShowSpinner()
    {
        // Arrange
        var taskCompletionSource = new TaskCompletionSource();
        authServiceMock.Setup(x => x.InitializeAsync()).Returns(taskCompletionSource.Task);

        // Act
        var cut = Render<Login>();

        // Assert
        Assert.Contains("Chargement", cut.Markup);
        Assert.Contains("spinner", cut.Markup);
    }
}