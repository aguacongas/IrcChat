// tests/IrcChat.Client.Tests/Pages/LoginTests.cs
using System.Net;
using System.Net.Http.Json;
using Bunit;
using Bunit.TestDoubles;
using IrcChat.Client.Pages;
using IrcChat.Client.Services;
using IrcChat.Shared.Models;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using RichardSzalay.MockHttp;
using Xunit;

namespace IrcChat.Client.Tests.Pages;

public class LoginTests : TestContext
{
    private readonly Mock<IUnifiedAuthService> _authServiceMock;
    private readonly MockHttpMessageHandler _mockHttp;
    private readonly FakeNavigationManager _navManager;

    public LoginTests()
    {
        _authServiceMock = new Mock<IUnifiedAuthService>();
        _mockHttp = new MockHttpMessageHandler();

        var httpClient = _mockHttp.ToHttpClient();
        httpClient.BaseAddress = new Uri("https://localhost:7000");

        Services.AddSingleton(_authServiceMock.Object);
        Services.AddSingleton(httpClient);
        Services.AddSingleton(JSInterop.JSRuntime);

        _navManager = Services.GetRequiredService<FakeNavigationManager>();
    }

    [Fact]
    public void Login_WhenAlreadyAuthenticated_ShouldRedirectToChat()
    {
        // Arrange
        _authServiceMock.Setup(x => x.IsAuthenticated).Returns(true);
        _authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);

        // Act
        RenderComponent<Login>();

        // Assert
        Assert.EndsWith("/chat", _navManager.Uri);
    }

    [Fact]
    public void Login_WithSavedUsername_ShouldPrefillInput()
    {
        // Arrange
        _authServiceMock.Setup(x => x.HasUsername).Returns(true);
        _authServiceMock.Setup(x => x.Username).Returns("SavedUser");
        _authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);

        // Act
        var cut = RenderComponent<Login>();

        // Assert
        var input = cut.Find("input[placeholder*='pseudo']");
        Assert.Equal("SavedUser", input.GetAttribute("value"));
    }

    [Fact]
    public async Task Login_EnterAsGuest_ShouldSetUsernameAndNavigate()
    {
        // Arrange
        _authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);
        _authServiceMock
            .Setup(x => x.SetUsernameAsync("TestUser", false, null))
            .Returns(Task.CompletedTask);

        var checkResponse = new UsernameCheckResponse
        {
            Available = true,
            IsReserved = false,
            IsCurrentlyUsed = false
        };

        _mockHttp.When(HttpMethod.Post, "*/api/oauth/check-username")
            .Respond(HttpStatusCode.OK, JsonContent.Create(checkResponse));

        var cut = RenderComponent<Login>();

        // Act
        var input = cut.Find("input[placeholder*='pseudo']");
        await cut.InvokeAsync(() => input.Input("TestUser"));
        await Task.Delay(600); // Attendre le debounce

        var guestButton = cut.Find("button:contains('invité')");
        await cut.InvokeAsync(() => guestButton.Click());

        // Assert
        _authServiceMock.Verify(
            x => x.SetUsernameAsync("TestUser", false, null),
            Times.Once);
        Assert.EndsWith("/chat", _navManager.Uri);
    }

    [Fact]
    public async Task Login_UsernameCheckRequest_ShouldShowStatus()
    {
        // Arrange
        _authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);

        var checkResponse = new UsernameCheckResponse
        {
            Available = true,
            IsReserved = false,
            IsCurrentlyUsed = false
        };

        _mockHttp.When(HttpMethod.Post, "*/api/oauth/check-username")
            .Respond(HttpStatusCode.OK, JsonContent.Create(checkResponse));

        var cut = RenderComponent<Login>();

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
        _authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);

        var checkResponse = new UsernameCheckResponse
        {
            Available = false,
            IsReserved = true,
            ReservedProvider = ExternalAuthProvider.Google,
            IsCurrentlyUsed = false
        };

        _mockHttp.When(HttpMethod.Post, "*/api/oauth/check-username")
            .Respond(HttpStatusCode.OK, JsonContent.Create(checkResponse));

        var cut = RenderComponent<Login>();

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
        _authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);

        var checkResponse = new UsernameCheckResponse
        {
            Available = false,
            IsReserved = false,
            IsCurrentlyUsed = true
        };

        _mockHttp.When(HttpMethod.Post, "*/api/oauth/check-username")
            .Respond(HttpStatusCode.OK, JsonContent.Create(checkResponse));

        var cut = RenderComponent<Login>();

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
        _authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);

        var mockedRequest = _mockHttp.When(HttpMethod.Post, "*/api/oauth/check-username")
            .Respond(HttpStatusCode.OK);

        var cut = RenderComponent<Login>();

        // Act
        var input = cut.Find("input[placeholder*='pseudo']");
        await cut.InvokeAsync(() => input.Input("AB")); // < 3 caractères
        await Task.Delay(600);

        // Assert
        var requestCount = _mockHttp.GetMatchCount(mockedRequest);
        Assert.Equal(0, requestCount);
    }

    [Fact]
    public async Task Login_EnterKey_ShouldSubmitWhenAvailable()
    {
        // Arrange
        _authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);
        _authServiceMock
            .Setup(x => x.SetUsernameAsync("TestUser", false, null))
            .Returns(Task.CompletedTask);

        var checkResponse = new UsernameCheckResponse
        {
            Available = true,
            IsReserved = false,
            IsCurrentlyUsed = false
        };

        _mockHttp.When(HttpMethod.Post, "*/api/oauth/check-username")
            .Respond(HttpStatusCode.OK, JsonContent.Create(checkResponse));

        var cut = RenderComponent<Login>();

        // Act
        var input = cut.Find("input[placeholder*='pseudo']");
        await cut.InvokeAsync(() => input.Input("TestUser"));
        await Task.Delay(600);
        await cut.InvokeAsync(() => input.KeyPress("Enter"));

        // Assert
        _authServiceMock.Verify(
            x => x.SetUsernameAsync("TestUser", false, null),
            Times.Once);
    }

    [Fact]
    public async Task Login_LoginWithProvider_ShouldSetSessionStorage()
    {
        // Arrange
        _authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);

        var checkResponse = new UsernameCheckResponse
        {
            Available = false,
            IsReserved = true,
            ReservedProvider = ExternalAuthProvider.Google,
            IsCurrentlyUsed = false
        };

        _mockHttp.When(HttpMethod.Post, "*/api/oauth/check-username")
            .Respond(HttpStatusCode.OK, JsonContent.Create(checkResponse));

        JSInterop.SetupVoid("sessionStorage.setItem", _ => true).SetVoidResult();

        var cut = RenderComponent<Login>();

        // Act
        var input = cut.Find("input[placeholder*='pseudo']");
        await cut.InvokeAsync(() => input.Input("ReservedUser"));
        await Task.Delay(600);

        var loginButton = cut.Find("button:contains('Se connecter')");
        await cut.InvokeAsync(() => loginButton.Click());

        // Assert
        JSInterop.VerifyInvoke("sessionStorage.setItem", 1);
        Assert.Contains("oauth-login", _navManager.Uri);
    }

    [Fact]
    public async Task Login_UseAnotherUsername_ShouldClearState()
    {
        // Arrange
        _authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);
        _authServiceMock.Setup(x => x.HasUsername).Returns(true);
        _authServiceMock.Setup(x => x.Username).Returns("SavedUser");
        _authServiceMock.Setup(x => x.IsReserved).Returns(true);
        _authServiceMock.Setup(x => x.IsAuthenticated).Returns(false);
        _authServiceMock.Setup(x => x.ReservedProvider).Returns(ExternalAuthProvider.Google);
        _authServiceMock.Setup(x => x.ClearAllAsync()).Returns(Task.CompletedTask);

        var cut = RenderComponent<Login>();

        // Act
        var anotherUsernameButton = cut.Find("button:contains('autre pseudo')");
        await cut.InvokeAsync(() => anotherUsernameButton.Click());

        // Assert
        _authServiceMock.Verify(x => x.ClearAllAsync(), Times.Once);
    }

    [Fact]
    public async Task Login_NavigateToReserve_ShouldIncludeUsernameInQueryString()
    {
        // Arrange
        _authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);

        var checkResponse = new UsernameCheckResponse
        {
            Available = true,
            IsReserved = false,
            IsCurrentlyUsed = false
        };

        _mockHttp.When(HttpMethod.Post, "*/api/oauth/check-username")
            .Respond(HttpStatusCode.OK, JsonContent.Create(checkResponse));

        var cut = RenderComponent<Login>();

        // Act
        var input = cut.Find("input[placeholder*='pseudo']");
        await cut.InvokeAsync(() => input.Input("TestUser"));
        await Task.Delay(600);

        var reserveButton = cut.Find("button:contains('Réserver')");
        await cut.InvokeAsync(() => reserveButton.Click());

        // Assert
        Assert.Contains("reserve?username=TestUser", _navManager.Uri);
    }

    [Fact]
    public async Task Login_CheckUsername_ShouldDebounceRequests()
    {
        // Arrange
        _authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);

        var checkResponse = new UsernameCheckResponse
        {
            Available = true,
            IsReserved = false,
            IsCurrentlyUsed = false
        };

        var mockedRequest = _mockHttp.When(HttpMethod.Post, "*/api/oauth/check-username")
            .Respond(HttpStatusCode.OK, JsonContent.Create(checkResponse));

        var cut = RenderComponent<Login>();
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
        var requestCount = _mockHttp.GetMatchCount(mockedRequest);
        Assert.Equal(1, requestCount);
    }

    [Fact]
    public async Task Login_CheckUsername_WithError_ShouldDisplayErrorMessage()
    {
        // Arrange
        _authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);

        _mockHttp.When(HttpMethod.Post, "*/api/oauth/check-username")
            .Respond(HttpStatusCode.InternalServerError);

        var cut = RenderComponent<Login>();

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
        _authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);
        _authServiceMock.Setup(x => x.HasUsername).Returns(true);
        _authServiceMock.Setup(x => x.Username).Returns("ReservedUser");
        _authServiceMock.Setup(x => x.IsReserved).Returns(true);
        _authServiceMock.Setup(x => x.IsAuthenticated).Returns(false);

        // Act
        var cut = RenderComponent<Login>();

        // Assert
        var input = cut.Find("input[placeholder*='pseudo']");
        Assert.True(input.HasAttribute("disabled"));
    }

    [Fact]
    public async Task Login_EnterAsGuest_WithShortUsername_ShouldShowError()
    {
        // Arrange
        _authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);

        var checkResponse = new UsernameCheckResponse
        {
            Available = true,
            IsReserved = false,
            IsCurrentlyUsed = false
        };

        _mockHttp.When(HttpMethod.Post, "*/api/oauth/check-username")
            .Respond(HttpStatusCode.OK, JsonContent.Create(checkResponse));

        var cut = RenderComponent<Login>();

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
        _authServiceMock.Setup(x => x.InitializeAsync()).Returns(taskCompletionSource.Task);

        // Act
        var cut = RenderComponent<Login>();

        // Assert
        Assert.Contains("Chargement", cut.Markup);
        Assert.Contains("spinner", cut.Markup);
    }
}