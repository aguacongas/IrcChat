// tests/IrcChat.Client.Tests/Services/UnifiedAuthService_SetClientCookieTests.cs
using System.Net;
using System.Net.Http.Json;
using IrcChat.Client.Services;
using IrcChat.Shared.Models;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using Moq;
using Moq.Protected;
using Xunit;

namespace IrcChat.Client.Tests.Services;

public class UnifiedAuthService_SetClientCookieTests
{
    private readonly Mock<ILocalStorageService> _localStorageMock;
    private readonly Mock<HttpMessageHandler> _httpHandlerMock;
    private readonly HttpClient _httpClient;
    private readonly Mock<IJSRuntime> _jsRuntimeMock;
    private readonly Mock<ILogger<UnifiedAuthService>> _loggerMock;

    public UnifiedAuthService_SetClientCookieTests()
    {
        _localStorageMock = new Mock<ILocalStorageService>();
        _httpHandlerMock = new Mock<HttpMessageHandler>();
        _httpClient = new HttpClient(_httpHandlerMock.Object)
        {
            BaseAddress = new Uri("https://api.test.com")
        };
        _jsRuntimeMock = new Mock<IJSRuntime>();
        _loggerMock = new Mock<ILogger<UnifiedAuthService>>();
    }

    [Fact]
    public async Task SetClientCookieAsync_FirstCall_SendsRequestToApi()
    {
        // Arrange
        var service = CreateService();
        var clientUserId = Guid.NewGuid().ToString();

        SetupJSModule(clientUserId);
        SetupHttpResponse(HttpStatusCode.OK);

        // Act
        await service.SetClientCookieAsync();

        // Assert
        VerifyHttpRequest("/api/oauth/set-client-cookie", Times.Once());
    }

    [Fact]
    public async Task SetClientCookieAsync_SecondCall_DoesNotSendRequest()
    {
        // Arrange
        var service = CreateService();
        var clientUserId = Guid.NewGuid().ToString();

        SetupJSModule(clientUserId);
        SetupHttpResponse(HttpStatusCode.OK);

        // Act
        await service.SetClientCookieAsync();
        await service.SetClientCookieAsync();
        await service.SetClientCookieAsync();

        // Assert - Devrait être appelé une seule fois
        VerifyHttpRequest("/api/oauth/set-client-cookie", Times.Once());
    }

    [Fact]
    public async Task SetClientCookieAsync_UsesCorrectClientUserId()
    {
        // Arrange
        var service = CreateService();
        var expectedClientUserId = "test-user-id-123";

        SetupJSModule(expectedClientUserId);

        HttpRequestMessage? capturedRequest = null;
        SetupHttpResponse(HttpStatusCode.OK, request => capturedRequest = request);

        // Act
        await service.SetClientCookieAsync();

        // Assert
        Assert.NotNull(capturedRequest);
        var content = await capturedRequest.Content!.ReadFromJsonAsync<SetClientCookieRequest>();
        Assert.NotNull(content);
        Assert.Equal(expectedClientUserId, content.ClientUserId);
    }

    [Fact]
    public async Task SetClientCookieAsync_ForOAuthUser_UsesUserId()
    {
        // Arrange
        var service = CreateService();
        var userId = Guid.NewGuid();

        // Simuler un utilisateur OAuth authentifié
        await service.SetAuthStateAsync(
            token: "test-token",
            username: "testuser",
            email: "test@example.com",
            avatarUrl: null,
            userId: userId,
            provider: IrcChat.Shared.Models.ExternalAuthProvider.Google,
            isAdmin: false
        );

        HttpRequestMessage? capturedRequest = null;
        SetupHttpResponse(HttpStatusCode.OK, request => capturedRequest = request);

        // Act
        await service.SetClientCookieAsync();

        // Assert
        Assert.NotNull(capturedRequest);
        var content = await capturedRequest.Content!.ReadFromJsonAsync<SetClientCookieRequest>();
        Assert.NotNull(content);
        Assert.Equal(userId.ToString(), content.ClientUserId);
    }

    [Fact]
    public async Task SetClientCookieAsync_WhenApiReturnsError_ThrowsException()
    {
        // Arrange
        var service = CreateService();
        var clientUserId = Guid.NewGuid().ToString();

        SetupJSModule(clientUserId);
        SetupHttpResponse(HttpStatusCode.InternalServerError);

        // Act & Assert
        await Assert.ThrowsAsync<HttpRequestException>(
            () => service.SetClientCookieAsync());
    }

    [Fact]
    public async Task SetClientCookieAsync_WhenApiFails_LogsError()
    {
        // Arrange
        var service = CreateService();
        var clientUserId = Guid.NewGuid().ToString();

        SetupJSModule(clientUserId);
        SetupHttpResponse(HttpStatusCode.InternalServerError);

        // Act
        try
        {
            await service.SetClientCookieAsync();
        }
        catch
        {
            // Ignorer l'exception pour tester le logging
        }

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Erreur lors de la définition du cookie client")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task SetClientCookieAsync_OnSuccess_LogsInformation()
    {
        // Arrange
        var service = CreateService();
        var clientUserId = Guid.NewGuid().ToString();

        SetupJSModule(clientUserId);
        SetupHttpResponse(HttpStatusCode.OK);

        // Act
        await service.SetClientCookieAsync();

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Cookie client défini avec succès")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task SetClientCookieAsync_WhenGetClientUserIdFails_ThrowsException()
    {
        // Arrange
        var service = CreateService();

        // Ne pas configurer le module JS pour provoquer une erreur
        _jsRuntimeMock
            .Setup(x => x.InvokeAsync<IJSObjectReference>(
                "import",
                It.IsAny<object[]>()))
            .ThrowsAsync(new JSException("Module not found"));

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.SetClientCookieAsync());
    }

    [Fact]
    public async Task SetClientCookieAsync_MultipleInstances_EachSendsRequestOnce()
    {
        // Arrange
        var service1 = CreateService();
        var service2 = CreateService();
        var clientUserId = Guid.NewGuid().ToString();

        SetupJSModule(clientUserId);
        SetupHttpResponse(HttpStatusCode.OK);

        // Act
        await service1.SetClientCookieAsync();
        await service1.SetClientCookieAsync(); // Ne devrait pas envoyer
        await service2.SetClientCookieAsync(); // Service différent, devrait envoyer
        await service2.SetClientCookieAsync(); // Ne devrait pas envoyer

        // Assert
        VerifyHttpRequest("/api/oauth/set-client-cookie", Times.Exactly(2));
    }

    [Fact]
    public async Task SetClientCookieAsync_SendsCorrectHttpMethod()
    {
        // Arrange
        var service = CreateService();
        var clientUserId = Guid.NewGuid().ToString();

        SetupJSModule(clientUserId);

        HttpRequestMessage? capturedRequest = null;
        SetupHttpResponse(HttpStatusCode.OK, request => capturedRequest = request);

        // Act
        await service.SetClientCookieAsync();

        // Assert
        Assert.NotNull(capturedRequest);
        Assert.Equal(HttpMethod.Post, capturedRequest.Method);
    }

    [Fact]
    public async Task SetClientCookieAsync_SendsJsonContent()
    {
        // Arrange
        var service = CreateService();
        var clientUserId = Guid.NewGuid().ToString();

        SetupJSModule(clientUserId);

        HttpRequestMessage? capturedRequest = null;
        SetupHttpResponse(HttpStatusCode.OK, request => capturedRequest = request);

        // Act
        await service.SetClientCookieAsync();

        // Assert
        Assert.NotNull(capturedRequest);
        Assert.NotNull(capturedRequest.Content);
        Assert.Equal("application/json", capturedRequest.Content.Headers.ContentType?.MediaType);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task SetClientCookieAsync_WithVariousErrorCodes_ThrowsException(HttpStatusCode statusCode)
    {
        // Arrange
        var service = CreateService();
        var clientUserId = Guid.NewGuid().ToString();

        SetupJSModule(clientUserId);
        SetupHttpResponse(statusCode);

        // Act & Assert
        await Assert.ThrowsAsync<HttpRequestException>(
            () => service.SetClientCookieAsync());
    }

    private UnifiedAuthService CreateService()
    {
        return new UnifiedAuthService(
            _localStorageMock.Object,
            _httpClient,
            _jsRuntimeMock.Object,
            _loggerMock.Object);
    }

    private void SetupJSModule(string clientUserId)
    {
        var jsModuleMock = new Mock<IJSObjectReference>();
        jsModuleMock
            .Setup(x => x.InvokeAsync<string>("getUserId", It.IsAny<object[]>()))
            .ReturnsAsync(clientUserId);

        _jsRuntimeMock
            .Setup(x => x.InvokeAsync<IJSObjectReference>(
                "import",
                It.Is<object[]>(args => args.Length > 0 && args[0].ToString() == "./js/userIdManager.js")))
            .ReturnsAsync(jsModuleMock.Object);
    }

    private void SetupHttpResponse(HttpStatusCode statusCode, Action<HttpRequestMessage>? requestCallback = null)
    {
        _httpHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((req, ct) => requestCallback?.Invoke(req))
            .ReturnsAsync(new HttpResponseMessage(statusCode));
    }

    private void VerifyHttpRequest(string expectedPath, Times times)
    {
        _httpHandlerMock
            .Protected()
            .Verify(
                "SendAsync",
                times,
                ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.PathAndQuery.Contains(expectedPath)),
                ItExpr.IsAny<CancellationToken>());
    }
}