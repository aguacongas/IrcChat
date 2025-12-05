// tests/IrcChat.Client.Tests/Pages/OAuthConnectTests.cs
using System.Net;
using System.Net.Http.Json;
using IrcChat.Client.Pages;
using IrcChat.Client.Services;
using IrcChat.Shared.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using RichardSzalay.MockHttp;

namespace IrcChat.Client.Tests.Pages;

public class OAuthConnectTests : BunitContext
{
    private readonly Mock<IUnifiedAuthService> authServiceMock;
    private readonly Mock<IOAuthClientService> oauthClientServiceMock;
    private readonly MockHttpMessageHandler mockHttp;
    private readonly NavigationManager navManager;

    public OAuthConnectTests()
    {
        mockHttp = new MockHttpMessageHandler();

        var httpClient = mockHttp.ToHttpClient();
        httpClient.BaseAddress = new Uri("https://localhost:7000");

        authServiceMock = new Mock<IUnifiedAuthService>();
        oauthClientServiceMock = new Mock<IOAuthClientService>();

        Services.AddSingleton(authServiceMock.Object);
        Services.AddSingleton(oauthClientServiceMock.Object);
        Services.AddSingleton(httpClient);
        Services.AddSingleton(JSInterop.JSRuntime);

        navManager = Services.GetRequiredService<NavigationManager>();
    }

    [Fact]
    public void OAuthConnect_WithError_ShouldShowError()
    {
        // Arrange & Act
        navManager.NavigateTo(navManager.GetUriWithQueryParameters(new Dictionary<string, object?>
        {
            ["error"] = "access_denied",
        }));

        var cut = Render<OAuthConnect>();

        // Assert
        Assert.Contains("Erreur OAuth", cut.Markup);
        Assert.Contains("access_denied", cut.Markup);
    }

    [Fact]
    public void OAuthConnect_WithoutParams_ShouldRedirectToLogin()
    {
        // Arrange
        authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);

        // Act
        Render<OAuthConnect>();

        // Assert
        Assert.EndsWith("/login", navManager.Uri);
    }

    [Fact]
    public async Task OAuthConnect_InitiateFlow_ShouldGenerateAuthUrl()
    {
        // Arrange
        authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);

        JSInterop.SetupVoid("sessionStorage.setItem", _ => true).SetVoidResult();

        oauthClientServiceMock
            .Setup(x => x.InitiateAuthorizationFlowAsync(
                ExternalAuthProvider.Google,
                It.IsAny<string>()))
            .ReturnsAsync("https://accounts.google.com/o/oauth2/auth?client_id=test");

        // Act
        navManager.NavigateTo(navManager.GetUriWithQueryParameters(new Dictionary<string, object?>
        {
            ["provider"] = "Google",
            ["mode"] = "reserve",
        }));
        Render<OAuthConnect>();

        await Task.Delay(300);

        // Assert
        Assert.Contains("google.com", navManager.Uri);
    }

    [Fact]
    public async Task OAuthConnect_HandleCallback_Reserve_ShouldCompleteReservation()
    {
        // Arrange
        var userId = Guid.NewGuid();
        authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);
        authServiceMock.Setup(x => x.SetAuthStateAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<Guid>(),
            It.IsAny<ExternalAuthProvider>(),
            It.IsAny<bool>()))
            .Returns(Task.CompletedTask);

        JSInterop.Setup<string>("sessionStorage.getItem", "oauth_mode")
            .SetResult("reserve");
        JSInterop.Setup<string>("sessionStorage.getItem", "temp_username_to_reserve")
            .SetResult("TestUser");
        JSInterop.Setup<string>("sessionStorage.getItem", "oauth_provider")
            .SetResult("Google");
        JSInterop.Setup<string>("sessionStorage.getItem", "oauth_code_verifier")
            .SetResult("test_verifier");
        JSInterop.Setup<string>("sessionStorage.getItem", "temp_user_id")
            .SetResult(userId.ToString());

        JSInterop.SetupVoid("sessionStorage.removeItem", _ => true).SetVoidResult();

        var reserveResponse = new OAuthLoginResponse
        {
            Token = "test-token",
            Username = "TestUser",
            Email = "test@example.com",
            UserId = userId,
            IsNewUser = true,
            IsAdmin = false,
        };

        mockHttp.When(HttpMethod.Post, "*/api/oauth/reserve-username")
            .Respond(HttpStatusCode.OK, JsonContent.Create(reserveResponse));

        // Act
        navManager.NavigateTo(navManager.GetUriWithQueryParameters(new Dictionary<string, object?>
        {
            ["code"] = "auth_code_123",
            ["state"] = "random_state",
        }));

        Render<OAuthConnect>();

        await Task.Delay(500);

        // Assert
        Assert.EndsWith("/chat", navManager.Uri);
        authServiceMock.Verify(
            x => x.SetAuthStateAsync(
                "test-token",
                "TestUser",
                "test@example.com",
                It.IsAny<string>(),
                userId,
                ExternalAuthProvider.Google,
                false),
            Times.Once);
    }

    [Fact]
    public async Task OAuthConnect_HandleCallback_Reserve_ShouldPassUserIdToEndpoint()
    {
        // Arrange
        var userId = Guid.NewGuid();
        authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);
        authServiceMock.Setup(x => x.SetAuthStateAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<Guid>(),
            It.IsAny<ExternalAuthProvider>(),
            It.IsAny<bool>()))
            .Returns(Task.CompletedTask);

        JSInterop.Setup<string>("sessionStorage.getItem", "oauth_mode")
            .SetResult("reserve");
        JSInterop.Setup<string>("sessionStorage.getItem", "temp_username_to_reserve")
            .SetResult("NewUser");
        JSInterop.Setup<string>("sessionStorage.getItem", "oauth_provider")
            .SetResult("Google");
        JSInterop.Setup<string>("sessionStorage.getItem", "oauth_code_verifier")
            .SetResult("test_verifier");
        JSInterop.Setup<string>("sessionStorage.getItem", "temp_user_id")
            .SetResult(userId.ToString());

        JSInterop.SetupVoid("sessionStorage.removeItem", _ => true).SetVoidResult();

        var reserveResponse = new OAuthLoginResponse
        {
            Token = "test-token",
            Username = "NewUser",
            Email = "new@example.com",
            UserId = userId,
            IsNewUser = true,
            IsAdmin = false,
        };

        var reserveRequest = mockHttp.When(HttpMethod.Post, "*/api/oauth/reserve-username")
            .Respond(HttpStatusCode.OK, JsonContent.Create(reserveResponse));

        // Act
        navManager.NavigateTo(navManager.GetUriWithQueryParameters(new Dictionary<string, object?>
        {
            ["code"] = "auth_code_123",
            ["state"] = "random_state",
        }));

        Render<OAuthConnect>();

        await Task.Delay(500);

        // Assert
        var request = mockHttp.GetMatchCount(reserveRequest);
        Assert.Equal(1, request);
    }

    [Fact]
    public async Task OAuthConnect_HandleCallback_Reserve_WithMissingUserId_ShouldShowError()
    {
        // Arrange
        authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);

        JSInterop.Setup<string>("sessionStorage.getItem", "oauth_mode")
            .SetResult("reserve");
        JSInterop.Setup<string>("sessionStorage.getItem", "temp_username_to_reserve")
            .SetResult("TestUser");
        JSInterop.Setup<string>("sessionStorage.getItem", "oauth_provider")
            .SetResult("Google");
        JSInterop.Setup<string>("sessionStorage.getItem", "oauth_code_verifier")
            .SetResult("test_verifier");
        JSInterop.Setup<string>("sessionStorage.getItem", "temp_user_id")
            .SetResult(null!); // 👈 UserId manquant

        JSInterop.SetupVoid("sessionStorage.removeItem", _ => true).SetVoidResult();

        // Act
        navManager.NavigateTo(navManager.GetUriWithQueryParameters(new Dictionary<string, object?>
        {
            ["code"] = "auth_code_123",
            ["state"] = "random_state",
        }));

        var cut = Render<OAuthConnect>();

        await Task.Delay(300);

        // Assert
        Assert.Contains("invalide", cut.Markup);
    }

    [Fact]
    public async Task OAuthConnect_HandleCallback_Reserve_WithInvalidUserId_ShouldShowError()
    {
        // Arrange
        authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);

        JSInterop.Setup<string>("sessionStorage.getItem", "oauth_mode")
            .SetResult("reserve");
        JSInterop.Setup<string>("sessionStorage.getItem", "temp_username_to_reserve")
            .SetResult("TestUser");
        JSInterop.Setup<string>("sessionStorage.getItem", "oauth_provider")
            .SetResult("Google");
        JSInterop.Setup<string>("sessionStorage.getItem", "oauth_code_verifier")
            .SetResult("test_verifier");
        JSInterop.Setup<string>("sessionStorage.getItem", "temp_user_id")
            .SetResult("not-a-guid"); // 👈 Format GUID invalide

        JSInterop.SetupVoid("sessionStorage.removeItem", _ => true).SetVoidResult();

        // Act
        navManager.NavigateTo(navManager.GetUriWithQueryParameters(new Dictionary<string, object?>
        {
            ["code"] = "auth_code_123",
            ["state"] = "random_state",
        }));

        var cut = Render<OAuthConnect>();

        await Task.Delay(300);

        // Assert
        Assert.Contains("invalide", cut.Markup);
    }

    [Fact]
    public async Task OAuthConnect_HandleCallback_Login_ShouldComplete()
    {
        // Arrange
        authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);
        authServiceMock.Setup(x => x.SetAuthStateAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<Guid>(),
            It.IsAny<ExternalAuthProvider>(),
            It.IsAny<bool>()))
            .Returns(Task.CompletedTask);

        JSInterop.Setup<string>("sessionStorage.getItem", "oauth_mode")
            .SetResult("login");
        JSInterop.Setup<string>("sessionStorage.getItem", "oauth_provider")
            .SetResult("Microsoft");
        JSInterop.Setup<string>("sessionStorage.getItem", "oauth_code_verifier")
            .SetResult("test_verifier");

        JSInterop.SetupVoid("sessionStorage.removeItem", _ => true).SetVoidResult();

        var loginResponse = new OAuthLoginResponse
        {
            Token = "login-token",
            Username = "ExistingUser",
            Email = "existing@example.com",
            UserId = Guid.NewGuid(),
            IsNewUser = false,
            IsAdmin = true,
        };

        mockHttp.When(HttpMethod.Post, "*/api/oauth/login-reserved")
            .Respond(HttpStatusCode.OK, JsonContent.Create(loginResponse));

        // Act
        navManager.NavigateTo(navManager.GetUriWithQueryParameters(new Dictionary<string, object?>
        {
            ["code"] = "auth_code_456",
            ["state"] = "xyz123",
        }));
        Render<OAuthConnect>();

        await Task.Delay(500);

        // Assert
        Assert.EndsWith("/chat", navManager.Uri);
        authServiceMock.Verify(
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
        // Arrange
        authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);
        oauthClientServiceMock.Setup(x => x.InitiateAuthorizationFlowAsync(It.IsAny<ExternalAuthProvider>(), It.IsAny<string>()))
            .ReturnsAsync("https://example.com/auth");
        JSInterop.SetupVoid("sessionStorage.setItem", _ => true).SetVoidResult();

        // Act
        navManager.NavigateTo(navManager.GetUriWithQueryParameters(new Dictionary<string, object?>
        {
            ["provider"] = "Google",
            ["mode"] = "reserve",
        }));

        var cut = Render<OAuthConnect>();

        // Assert
        Assert.Contains("Redirection vers Google...", cut.Markup);
    }

    [Fact]
    public async Task OAuthConnect_ReserveFailure_ShouldShowError()
    {
        // Arrange
        authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);

        JSInterop.Setup<string>("sessionStorage.getItem", "oauth_mode")
            .SetResult("reserve");
        JSInterop.Setup<string>("sessionStorage.getItem", "temp_username_to_reserve")
            .SetResult("TestUser");
        JSInterop.Setup<string>("sessionStorage.getItem", "oauth_provider")
            .SetResult("Google");
        JSInterop.Setup<string>("sessionStorage.getItem", "oauth_code_verifier")
            .SetResult("test_verifier");
        JSInterop.Setup<string>("sessionStorage.getItem", "temp_user_id")
            .SetResult(Guid.NewGuid().ToString());

        mockHttp.When(HttpMethod.Post, "*/api/oauth/reserve-username")
            .Respond(
                HttpStatusCode.BadRequest,
                new StringContent("{\"error\":\"username_taken\"}"));

        // Act
        navManager.NavigateTo(navManager.GetUriWithQueryParameters(new Dictionary<string, object?>
        {
            ["code"] = "auth_code_123",
            ["state"] = "random_state",
        }));

        var cut = Render<OAuthConnect>();

        await Task.Delay(500);

        // Assert
        Assert.Contains("Erreur", cut.Markup);
    }

    [Fact]
    public async Task OAuthConnect_LoginFailure_ShouldShowError()
    {
        // Arrange
        authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);

        JSInterop.Setup<string>("sessionStorage.getItem", "oauth_mode")
            .SetResult("login");
        JSInterop.Setup<string>("sessionStorage.getItem", "oauth_provider")
            .SetResult("Google");
        JSInterop.Setup<string>("sessionStorage.getItem", "oauth_code_verifier")
            .SetResult("test_verifier");

        mockHttp.When(HttpMethod.Post, "*/api/oauth/login-reserved")
            .Respond(
                HttpStatusCode.NotFound,
                new StringContent("{\"error\":\"not_found\"}"));

        // Act
        navManager.NavigateTo(navManager.GetUriWithQueryParameters(new Dictionary<string, object?>
        {
            ["code"] = "auth_code_456",
            ["state"] = "state_xyz",
        }));
        var cut = Render<OAuthConnect>();

        await Task.Delay(500);

        // Assert
        Assert.Contains("Erreur", cut.Markup);
    }

    [Fact]
    public async Task OAuthConnect_MissingSessionData_ShouldShowError()
    {
        // Arrange
        authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);

        JSInterop.Setup<string>("sessionStorage.getItem", "oauth_mode")
            .SetResult("reserve");
        JSInterop.Setup<string>("sessionStorage.getItem", "temp_username_to_reserve")
            .SetResult(null!); // Pas de username stocké
        JSInterop.Setup<string>("sessionStorage.getItem", "temp_user_id")
            .SetResult(Guid.NewGuid().ToString());

        // Act
        navManager.NavigateTo(navManager.GetUriWithQueryParameters(new Dictionary<string, object?>
        {
            ["code"] = "auth_code_123",
            ["state"] = "random_state",
        }));

        var cut = Render<OAuthConnect>();

        await Task.Delay(300);

        // Assert
        Assert.Contains("introuvable", cut.Markup);
    }

    [Fact]
    public async Task OAuthConnect_SessionStorageCleaned_AfterSuccessfulReserve()
    {
        // Arrange
        var userId = Guid.NewGuid();
        authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);
        authServiceMock.Setup(x => x.SetAuthStateAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<Guid>(),
            It.IsAny<ExternalAuthProvider>(),
            It.IsAny<bool>()))
            .Returns(Task.CompletedTask);

        JSInterop.Setup<string>("sessionStorage.getItem", "oauth_mode")
            .SetResult("reserve");
        JSInterop.Setup<string>("sessionStorage.getItem", "temp_username_to_reserve")
            .SetResult("TestUser");
        JSInterop.Setup<string>("sessionStorage.getItem", "oauth_provider")
            .SetResult("Google");
        JSInterop.Setup<string>("sessionStorage.getItem", "oauth_code_verifier")
            .SetResult("test_verifier");
        JSInterop.Setup<string>("sessionStorage.getItem", "temp_user_id")
            .SetResult(userId.ToString());

        JSInterop.SetupVoid("sessionStorage.removeItem", _ => true).SetVoidResult();

        var reserveResponse = new OAuthLoginResponse
        {
            Token = "test-token",
            Username = "TestUser",
            Email = "test@example.com",
            UserId = userId,
            IsNewUser = true,
            IsAdmin = false,
        };

        mockHttp.When(HttpMethod.Post, "*/api/oauth/reserve-username")
            .Respond(HttpStatusCode.OK, JsonContent.Create(reserveResponse));

        // Act
        navManager.NavigateTo(navManager.GetUriWithQueryParameters(new Dictionary<string, object?>
        {
            ["code"] = "auth_code_123",
            ["state"] = "random_state",
        }));

        Render<OAuthConnect>();

        await Task.Delay(500);

        // Assert
        JSInterop.VerifyInvoke("sessionStorage.removeItem", calledTimes: 6); // Vérifie que 5 items ont été supprimés
    }
}