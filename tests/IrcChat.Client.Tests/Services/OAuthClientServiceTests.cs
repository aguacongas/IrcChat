// tests/IrcChat.Client.Tests/Services/OAuthClientServiceTests.cs
using System.Net;
using System.Net.Http.Json;
using IrcChat.Client.Models;
using IrcChat.Client.Services;
using IrcChat.Shared.Models;
using Microsoft.JSInterop;
using RichardSzalay.MockHttp;

namespace IrcChat.Client.Tests.Services;

public class OAuthClientServiceTests
{
    private readonly Mock<IJSRuntime> jsRuntimeMock;
    private readonly MockHttpMessageHandler mockHttp;
    private readonly HttpClient httpClient;

    public OAuthClientServiceTests()
    {
        jsRuntimeMock = new Mock<IJSRuntime>();
        mockHttp = new MockHttpMessageHandler();
        httpClient = mockHttp.ToHttpClient();
        httpClient.BaseAddress = new Uri("https://localhost:7000");
    }

    [Fact]
    public async Task InitiateAuthorizationFlowAsync_ShouldGenerateAuthUrlWithPKCE()
    {
        // Arrange
        var providerConfig = new OAuthProviderConfig
        {
            Provider = ExternalAuthProvider.Google,
            AuthorizationEndpoint = "https://accounts.google.com/o/oauth2/auth",
            ClientId = "test-client-id",
            Scope = "openid profile email",
        };

        mockHttp.When(HttpMethod.Get, "*/api/oauth/config/Google")
            .Respond(HttpStatusCode.OK, JsonContent.Create(providerConfig));

        jsRuntimeMock
            .Setup(x => x.InvokeAsync<IJSVoidResult>(
                "sessionStorage.setItem",
                It.IsAny<object[]>()))
            .ReturnsAsync((IJSVoidResult)null!);

        var service = new OAuthClientService(jsRuntimeMock.Object, httpClient);

        // Act
        var authUrl = await service.InitiateAuthorizationFlowAsync(
            ExternalAuthProvider.Google,
            "https://localhost:7001/oauth-login");

        // Assert
        Assert.StartsWith("https://accounts.google.com/o/oauth2/auth", authUrl);
        Assert.Contains("client_id=test-client-id", authUrl);
        Assert.Contains("redirect_uri=https%3A%2F%2Flocalhost%3A7001%2Foauth-login", authUrl);
        Assert.Contains("response_type=code", authUrl);
        Assert.Contains("scope=openid%20profile%20email", authUrl);
        Assert.Contains("state=", authUrl);
        Assert.Contains("code_challenge=", authUrl);
        Assert.Contains("code_challenge_method=S256", authUrl);

        jsRuntimeMock.Verify(
            x => x.InvokeAsync<object>(
                "sessionStorage.setItem",
                It.Is<object[]>(args =>
                    args.Length == 2 &&
                    args[0].ToString() == "oauth_state")),
            Times.Once);

        jsRuntimeMock.Verify(
            x => x.InvokeAsync<object>(
                "sessionStorage.setItem",
                It.Is<object[]>(args =>
                    args.Length == 2 &&
                    args[0].ToString() == "oauth_code_verifier")),
            Times.Once);

        jsRuntimeMock.Verify(
            x => x.InvokeAsync<object>(
                "sessionStorage.setItem",
                It.Is<object[]>(args =>
                    args.Length == 2 &&
                    args[0].ToString() == "oauth_provider" &&
                    args[1].ToString() == "Google")),
            Times.Once);
    }

    [Fact]
    public async Task InitiateAuthorizationFlowAsync_WhenApiCallFails_ShouldThrow()
    {
        // Arrange
        mockHttp.When(HttpMethod.Get, "*/api/oauth/config/Google")
            .Respond(HttpStatusCode.NotFound);

        var service = new OAuthClientService(jsRuntimeMock.Object, httpClient);

        // Act & Assert
        await Assert.ThrowsAsync<HttpRequestException>(async () =>
            await service.InitiateAuthorizationFlowAsync(
                ExternalAuthProvider.Google,
                "https://localhost:7001/oauth-login"));
    }

    [Fact]
    public async Task HandleCallbackAsync_WithValidState_ShouldExchangeToken()
    {
        // Arrange
        jsRuntimeMock
            .Setup(x => x.InvokeAsync<string>(
                "sessionStorage.getItem",
                It.Is<object[]>(args => args.Length == 1 && args[0].ToString() == "oauth_state")))
            .ReturnsAsync("saved-state");

        jsRuntimeMock
            .Setup(x => x.InvokeAsync<string>(
                "sessionStorage.getItem",
                It.Is<object[]>(args => args.Length == 1 && args[0].ToString() == "oauth_provider")))
            .ReturnsAsync("Google");

        jsRuntimeMock
            .Setup(x => x.InvokeAsync<string>(
                "sessionStorage.getItem",
                It.Is<object[]>(args => args.Length == 1 && args[0].ToString() == "oauth_code_verifier")))
            .ReturnsAsync("test-verifier");

        jsRuntimeMock
            .Setup(x => x.InvokeAsync<IJSVoidResult>(
                "sessionStorage.removeItem",
                It.IsAny<object[]>()))
            .ReturnsAsync((IJSVoidResult)null!);

        var loginResponse = new OAuthLoginResponse
        {
            Token = "test-token",
            Username = "TestUser",
            Email = "test@example.com",
            UserId = Guid.NewGuid(),
            IsNewUser = false,
            IsAdmin = false,
        };

        mockHttp.When(HttpMethod.Post, "*/api/oauth/token")
            .Respond(HttpStatusCode.OK, JsonContent.Create(loginResponse));

        var service = new OAuthClientService(jsRuntimeMock.Object, httpClient);

        // Act
        var result = await service.HandleCallbackAsync(
            "auth-code-123",
            "saved-state",
            "https://localhost:7001/oauth-login");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("test-token", result!.Token);
        Assert.Equal("TestUser", result.Username);
        Assert.Equal("test@example.com", result.Email);

        jsRuntimeMock.Verify(
            x => x.InvokeAsync<object>(
                "sessionStorage.removeItem",
                It.Is<object[]>(args => args.Length == 1 && args[0].ToString() == "oauth_state")),
            Times.Once);

        jsRuntimeMock.Verify(
            x => x.InvokeAsync<object>(
                "sessionStorage.removeItem",
                It.Is<object[]>(args => args.Length == 1 && args[0].ToString() == "oauth_code_verifier")),
            Times.Once);

        jsRuntimeMock.Verify(
            x => x.InvokeAsync<object>(
                "sessionStorage.removeItem",
                It.Is<object[]>(args => args.Length == 1 && args[0].ToString() == "oauth_provider")),
            Times.Once);
    }

    [Fact]
    public async Task HandleCallbackAsync_WithInvalidState_ShouldThrow()
    {
        // Arrange
        jsRuntimeMock
            .Setup(x => x.InvokeAsync<string>(
                "sessionStorage.getItem",
                It.Is<object[]>(args => args.Length == 1 && args[0].ToString() == "oauth_state")))
            .ReturnsAsync("saved-state");

        var service = new OAuthClientService(jsRuntimeMock.Object, httpClient);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await service.HandleCallbackAsync("code", "wrong-state", "https://localhost:7001/oauth-login"));
    }

    [Fact]
    public async Task HandleCallbackAsync_WithInvalidProvider_ShouldThrow()
    {
        // Arrange
        jsRuntimeMock
            .Setup(x => x.InvokeAsync<string>(
                "sessionStorage.getItem",
                It.Is<object[]>(args => args.Length == 1 && args[0].ToString() == "oauth_state")))
            .ReturnsAsync("saved-state");

        jsRuntimeMock
            .Setup(x => x.InvokeAsync<string>(
                "sessionStorage.getItem",
                It.Is<object[]>(args => args.Length == 1 && args[0].ToString() == "oauth_provider")))
            .ReturnsAsync("InvalidProvider");

        var service = new OAuthClientService(jsRuntimeMock.Object, httpClient);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await service.HandleCallbackAsync("code", "saved-state", "https://localhost:7001/oauth-login"));
    }

    [Fact]
    public async Task HandleCallbackAsync_WhenTokenExchangeFails_ShouldThrow()
    {
        // Arrange
        jsRuntimeMock
            .Setup(x => x.InvokeAsync<string>(
                "sessionStorage.getItem",
                It.Is<object[]>(args => args.Length == 1 && args[0].ToString() == "oauth_state")))
            .ReturnsAsync("saved-state");

        jsRuntimeMock
            .Setup(x => x.InvokeAsync<string>(
                "sessionStorage.getItem",
                It.Is<object[]>(args => args.Length == 1 && args[0].ToString() == "oauth_provider")))
            .ReturnsAsync("Google");

        jsRuntimeMock
            .Setup(x => x.InvokeAsync<string>(
                "sessionStorage.getItem",
                It.Is<object[]>(args => args.Length == 1 && args[0].ToString() == "oauth_code_verifier")))
            .ReturnsAsync("test-verifier");

        mockHttp.When(HttpMethod.Post, "*/api/oauth/token")
            .Respond(HttpStatusCode.BadRequest, new StringContent("Invalid grant"));

        var service = new OAuthClientService(jsRuntimeMock.Object, httpClient);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await service.HandleCallbackAsync(
                "invalid-code",
                "saved-state",
                "https://localhost:7001/oauth-login"));
    }

    [Fact]
    public async Task InitiateAuthorizationFlowAsync_ForMicrosoft_ShouldGenerateCorrectUrl()
    {
        // Arrange
        var providerConfig = new OAuthProviderConfig
        {
            Provider = ExternalAuthProvider.Microsoft,
            AuthorizationEndpoint = "https://login.microsoftonline.com/common/oauth2/v2.0/authorize",
            ClientId = "microsoft-client-id",
            Scope = "openid profile email",
        };

        mockHttp.When(HttpMethod.Get, "*/api/oauth/config/Microsoft")
            .Respond(HttpStatusCode.OK, JsonContent.Create(providerConfig));

        jsRuntimeMock
            .Setup(x => x.InvokeAsync<IJSVoidResult>(
                "sessionStorage.setItem",
                It.IsAny<object[]>()))
            .ReturnsAsync((IJSVoidResult)null!);

        var service = new OAuthClientService(jsRuntimeMock.Object, httpClient);

        // Act
        var authUrl = await service.InitiateAuthorizationFlowAsync(
            ExternalAuthProvider.Microsoft,
            "https://localhost:7001/oauth-login");

        // Assert
        Assert.StartsWith("https://login.microsoftonline.com/common/oauth2/v2.0/authorize", authUrl);
        Assert.Contains("client_id=microsoft-client-id", authUrl);
    }

    [Fact]
    public async Task HandleCallbackAsync_ShouldSendCorrectTokenRequest()
    {
        // Arrange
        jsRuntimeMock
            .Setup(x => x.InvokeAsync<string>(
                "sessionStorage.getItem",
                It.Is<object[]>(args => args.Length == 1 && args[0].ToString() == "oauth_state")))
            .ReturnsAsync("test-state");

        jsRuntimeMock
            .Setup(x => x.InvokeAsync<string>(
                "sessionStorage.getItem",
                It.Is<object[]>(args => args.Length == 1 && args[0].ToString() == "oauth_provider")))
            .ReturnsAsync("Facebook");

        jsRuntimeMock
            .Setup(x => x.InvokeAsync<string>(
                "sessionStorage.getItem",
                It.Is<object[]>(args => args.Length == 1 && args[0].ToString() == "oauth_code_verifier")))
            .ReturnsAsync("verifier-123");

        jsRuntimeMock
            .Setup(x => x.InvokeAsync<IJSVoidResult>(
                "sessionStorage.removeItem",
                It.IsAny<object[]>()))
            .ReturnsAsync((IJSVoidResult)null!);

        var loginResponse = new OAuthLoginResponse
        {
            Token = "fb-token",
            Username = "FacebookUser",
            Email = "fb@example.com",
            UserId = Guid.NewGuid(),
            IsNewUser = true,
            IsAdmin = false,
        };

        var mockedRequest = mockHttp.When(HttpMethod.Post, "*/api/oauth/token")
            .Respond(HttpStatusCode.OK, JsonContent.Create(loginResponse));

        var service = new OAuthClientService(jsRuntimeMock.Object, httpClient);

        // Act
        await service.HandleCallbackAsync(
            "fb-code",
            "test-state",
            "https://localhost:7001/oauth-login");

        // Assert
        Assert.Equal(1, mockHttp.GetMatchCount(mockedRequest));
    }
}