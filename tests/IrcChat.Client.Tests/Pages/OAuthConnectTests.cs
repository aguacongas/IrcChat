using System.Net;
using System.Net.Http.Json;
using Bunit;
using Bunit.TestDoubles;
using FluentAssertions;
using IrcChat.Client.Pages;
using IrcChat.Client.Services;
using IrcChat.Shared.Models;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using RichardSzalay.MockHttp;
using Xunit;

namespace IrcChat.Client.Tests.Pages;

public class OAuthConnectTests : TestContext
{
    private readonly Mock<UnifiedAuthService> _authServiceMock;
    private readonly Mock<OAuthClientService> _oauthClientServiceMock;
    private readonly MockHttpMessageHandler _mockHttp;

    public OAuthConnectTests()
    {
        // Mock HTTP
        _mockHttp = new MockHttpMessageHandler();
        var httpClient = _mockHttp.ToHttpClient();
        httpClient.BaseAddress = new Uri("https://localhost:7000");

        // Mock des services
        var localStorageMock = new Mock<LocalStorageService>(JSInterop.JSRuntime);
        _authServiceMock = new Mock<UnifiedAuthService>(localStorageMock.Object, httpClient);
        _oauthClientServiceMock = new Mock<OAuthClientService>(JSInterop.JSRuntime, httpClient);

        // Configuration par défaut
        _authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);

        // Enregistrement des services
        Services.AddSingleton(_authServiceMock.Object);
        Services.AddSingleton(_oauthClientServiceMock.Object);
        Services.AddSingleton(httpClient);
    }

    [Fact]
    public void OAuthConnect_WithError_ShouldShowError()
    {
        // Arrange & Act
        var cut = RenderComponent<OAuthConnect>(parameters => parameters
            .Add(p => p.ErrorParam, "access_denied"));

        // Assert
        cut.Markup.Should().Contain("Erreur OAuth");
        cut.Markup.Should().Contain("access_denied");
    }

    [Fact]
    public void OAuthConnect_WithoutParams_ShouldRedirectToLogin()
    {
        // Arrange
        var navMan = Services.GetRequiredService<FakeNavigationManager>();

        // Act
        var cut = RenderComponent<OAuthConnect>();

        // Assert
        navMan.Uri.Should().Contain("/login");
    }

    [Fact]
    public async Task OAuthConnect_InitiateFlow_ShouldGenerateAuthUrl()
    {
        // Arrange
        var navMan = Services.GetRequiredService<FakeNavigationManager>();

        JSInterop.Setup<object>("sessionStorage.setItem", _ => true)
            .SetResult(new object());

        _oauthClientServiceMock
            .Setup(x => x.InitiateAuthorizationFlowAsync(
                ExternalAuthProvider.Google,
                It.IsAny<string>()))
            .ReturnsAsync("https://accounts.google.com/o/oauth2/auth?client_id=test");

        // Act
        var cut = RenderComponent<OAuthConnect>(parameters => parameters
            .Add(p => p.ProviderParam, "Google")
            .Add(p => p.ModeParam, "reserve"));

        await Task.Delay(300); // Attendre l'initialisation

        // Assert
        navMan.Uri.Should().Contain("google.com");
    }

    [Fact]
    public async Task OAuthConnect_HandleCallback_Reserve_ShouldCompleteReservation()
    {
        // Arrange
        var navMan = Services.GetRequiredService<FakeNavigationManager>();
        navMan.NavigateTo("https://localhost/oauth-login");

        // Configuration du JSInterop pour sessionStorage
        JSInterop.Setup<string>("sessionStorage.getItem", "oauth_mode")
            .SetResult("reserve");
        JSInterop.Setup<string>("sessionStorage.getItem", "temp_username_to_reserve")
            .SetResult("TestUser");
        JSInterop.Setup<string>("sessionStorage.getItem", "oauth_provider")
            .SetResult("Google");
        JSInterop.Setup<string>("sessionStorage.getItem", "oauth_code_verifier")
            .SetResult("test_verifier");

        JSInterop.SetupVoid("sessionStorage.removeItem", _ => true);

        // Configuration de la réponse HTTP
        var reserveResponse = new OAuthLoginResponse
        {
            Token = "test-token",
            Username = "TestUser",
            Email = "test@example.com",
            UserId = Guid.NewGuid(),
            IsNewUser = true,
            IsAdmin = false
        };

        _mockHttp.When(HttpMethod.Post, "*/api/oauth/reserve-username")
            .Respond(HttpStatusCode.OK, JsonContent.Create(reserveResponse));

        // Act
        var cut = RenderComponent<OAuthConnect>(parameters => parameters
            .Add(p => p.CodeParam, "auth_code_123")
            .Add(p => p.StateParam, "random_state"));

        await Task.Delay(500); // Attendre le traitement

        // Assert
        navMan.Uri.Should().Contain("/chat");
        _authServiceMock.Verify(
            x => x.SetAuthStateAsync(
                "test-token",
                "TestUser",
                "test@example.com",
                It.IsAny<string>(),
                It.IsAny<Guid>(),
                ExternalAuthProvider.Google,
                false),
            Times.Once);
    }

    [Fact]
    public async Task OAuthConnect_HandleCallback_Login_ShouldComplete()
    {
        // Arrange
        var navMan = Services.GetRequiredService<FakeNavigationManager>();
        navMan.NavigateTo("https://localhost/oauth-login");

        JSInterop.Setup<string>("sessionStorage.getItem", "oauth_mode")
            .SetResult("login");
        JSInterop.Setup<string>("sessionStorage.getItem", "oauth_provider")
            .SetResult("Microsoft");
        JSInterop.Setup<string>("sessionStorage.getItem", "oauth_code_verifier")
            .SetResult("test_verifier");

        JSInterop.SetupVoid("sessionStorage.removeItem", _ => true);

        var loginResponse = new OAuthLoginResponse
        {
            Token = "login-token",
            Username = "ExistingUser",
            Email = "existing@example.com",
            UserId = Guid.NewGuid(),
            IsNewUser = false,
            IsAdmin = true
        };

        _mockHttp.When(HttpMethod.Post, "*/api/oauth/login-reserved")
            .Respond(HttpStatusCode.OK, JsonContent.Create(loginResponse));

        // Act
        var cut = RenderComponent<OAuthConnect>(parameters => parameters
            .Add(p => p.CodeParam, "auth_code_456")
            .Add(p => p.StateParam, "state_xyz"));

        await Task.Delay(500);

        // Assert
        navMan.Uri.Should().Contain("/chat");
        _authServiceMock.Verify(
            x => x.SetAuthStateAsync(
                "login-token",
                "ExistingUser",
                "existing@example.com",
                It.IsAny<string>(),
                It.IsAny<Guid>(),
                ExternalAuthProvider.Microsoft,
                true),
            Times.Once);
    }

    [Fact]
    public void OAuthConnect_ShowsLoadingMessage()
    {
        // Arrange & Act
        var cut = RenderComponent<OAuthConnect>(parameters => parameters
            .Add(p => p.ProviderParam, "Google")
            .Add(p => p.ModeParam, "reserve"));

        // Assert
        cut.Markup.Should().Contain("Connexion en cours");
    }

    [Fact]
    public async Task OAuthConnect_ReserveFailure_ShouldShowError()
    {
        // Arrange
        JSInterop.Setup<string>("sessionStorage.getItem", "oauth_mode")
            .SetResult("reserve");
        JSInterop.Setup<string>("sessionStorage.getItem", "temp_username_to_reserve")
            .SetResult("TestUser");
        JSInterop.Setup<string>("sessionStorage.getItem", "oauth_provider")
            .SetResult("Google");
        JSInterop.Setup<string>("sessionStorage.getItem", "oauth_code_verifier")
            .SetResult("test_verifier");

        _mockHttp.When(HttpMethod.Post, "*/api/oauth/reserve-username")
            .Respond(HttpStatusCode.BadRequest, "text/plain", "username_taken");

        // Act
        var cut = RenderComponent<OAuthConnect>(parameters => parameters
            .Add(p => p.CodeParam, "auth_code_123")
            .Add(p => p.StateParam, "random_state"));

        await Task.Delay(500);

        // Assert
        cut.Markup.Should().Contain("Erreur");
    }
}