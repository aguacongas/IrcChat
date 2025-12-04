// tests/IrcChat.Client.Tests/Services/CredentialsHandlerTests.cs
using System.Net;
using IrcChat.Client.Services;
using Moq;
using Moq.Protected;
using Xunit;

namespace IrcChat.Client.Tests.Services;

public class CredentialsHandlerTests
{
    private readonly Mock<IUnifiedAuthService> _authServiceMock;
    private readonly Mock<HttpMessageHandler> _innerHandlerMock;
    private readonly CredentialsHandler _handler;
    private static readonly string[] _expected = ["Initialize", "SetCookie", "SendRequest"];

    public CredentialsHandlerTests()
    {
        _authServiceMock = new Mock<IUnifiedAuthService>();
        _innerHandlerMock = new Mock<HttpMessageHandler>();

        _handler = new CredentialsHandler(_authServiceMock.Object)
        {
            InnerHandler = _innerHandlerMock.Object
        };
    }

    [Fact]
    public async Task SendAsync_CallsInitializeAsync()
    {
        // Arrange
        var request = new HttpRequestMessage(HttpMethod.Get, "https://api.test.com/test");

        _authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);
        _authServiceMock.Setup(x => x.SetClientCookieAsync()).Returns(Task.CompletedTask);
        _authServiceMock.Setup(x => x.IsAuthenticated).Returns(false);

        SetupInnerHandler(HttpStatusCode.OK);

        var invoker = new HttpMessageInvoker(_handler);

        // Act
        await invoker.SendAsync(request, CancellationToken.None);

        // Assert
        _authServiceMock.Verify(x => x.InitializeAsync(), Times.Once);
    }

    [Fact]
    public async Task SendAsync_CallsSetClientCookieAsync()
    {
        // Arrange
        var request = new HttpRequestMessage(HttpMethod.Get, "https://api.test.com/test");

        _authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);
        _authServiceMock.Setup(x => x.SetClientCookieAsync()).Returns(Task.CompletedTask);
        _authServiceMock.Setup(x => x.IsAuthenticated).Returns(false);

        SetupInnerHandler(HttpStatusCode.OK);

        var invoker = new HttpMessageInvoker(_handler);

        // Act
        await invoker.SendAsync(request, CancellationToken.None);

        // Assert
        _authServiceMock.Verify(x => x.SetClientCookieAsync(), Times.Once);
    }

    [Fact]
    public async Task SendAsync_WhenAuthenticated_AddsBearerToken()
    {
        // Arrange
        var token = "test-jwt-token-123";
        var request = new HttpRequestMessage(HttpMethod.Get, "https://api.test.com/test");

        _authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);
        _authServiceMock.Setup(x => x.SetClientCookieAsync()).Returns(Task.CompletedTask);
        _authServiceMock.Setup(x => x.IsAuthenticated).Returns(true);
        _authServiceMock.Setup(x => x.Token).Returns(token);

        SetupInnerHandler(HttpStatusCode.OK);

        var invoker = new HttpMessageInvoker(_handler);

        // Act
        await invoker.SendAsync(request, CancellationToken.None);

        // Assert
        Assert.NotNull(request.Headers.Authorization);
        Assert.Equal("Bearer", request.Headers.Authorization.Scheme);
        Assert.Equal(token, request.Headers.Authorization.Parameter);
    }

    [Fact]
    public async Task SendAsync_WhenNotAuthenticated_DoesNotAddToken()
    {
        // Arrange
        var request = new HttpRequestMessage(HttpMethod.Get, "https://api.test.com/test");

        _authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);
        _authServiceMock.Setup(x => x.SetClientCookieAsync()).Returns(Task.CompletedTask);
        _authServiceMock.Setup(x => x.IsAuthenticated).Returns(false);

        SetupInnerHandler(HttpStatusCode.OK);

        var invoker = new HttpMessageInvoker(_handler);

        // Act
        await invoker.SendAsync(request, CancellationToken.None);

        // Assert
        Assert.Null(request.Headers.Authorization);
    }

    [Fact]
    public async Task SendAsync_WhenTokenIsNull_DoesNotAddToken()
    {
        // Arrange
        var request = new HttpRequestMessage(HttpMethod.Get, "https://api.test.com/test");

        _authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);
        _authServiceMock.Setup(x => x.SetClientCookieAsync()).Returns(Task.CompletedTask);
        _authServiceMock.Setup(x => x.IsAuthenticated).Returns(true);
        _authServiceMock.Setup(x => x.Token).Returns((string?)null);

        SetupInnerHandler(HttpStatusCode.OK);

        var invoker = new HttpMessageInvoker(_handler);

        // Act
        await invoker.SendAsync(request, CancellationToken.None);

        // Assert
        Assert.Null(request.Headers.Authorization);
    }

    [Fact]
    public async Task SendAsync_SetsBrowserRequestCredentialsToInclude()
    {
        // Arrange
        var request = new HttpRequestMessage(HttpMethod.Get, "https://api.test.com/test");

        _authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);
        _authServiceMock.Setup(x => x.SetClientCookieAsync()).Returns(Task.CompletedTask);
        _authServiceMock.Setup(x => x.IsAuthenticated).Returns(false);

        SetupInnerHandler(HttpStatusCode.OK);

        var invoker = new HttpMessageInvoker(_handler);

        // Act
        await invoker.SendAsync(request, CancellationToken.None);

        // Assert
        // Vérifier que l'option "credentials" est définie
#pragma warning disable CS0252 // Possible unintended reference comparison; left hand side needs cast
        Assert.Contains(request.Options, option => option.Key == "WebAssemblyFetchOptions"
            && option.Value is IDictionary<string, object> values
            && values["credentials"] == "include");
#pragma warning restore CS0252 // Possible unintended reference comparison; left hand side needs cast
    }

    [Fact]
    public async Task SendAsync_CallsInnerHandler()
    {
        // Arrange
        var request = new HttpRequestMessage(HttpMethod.Get, "https://api.test.com/test");

        _authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);
        _authServiceMock.Setup(x => x.SetClientCookieAsync()).Returns(Task.CompletedTask);
        _authServiceMock.Setup(x => x.IsAuthenticated).Returns(false);

        SetupInnerHandler(HttpStatusCode.OK);

        var invoker = new HttpMessageInvoker(_handler);

        // Act
        var response = await invoker.SendAsync(request, CancellationToken.None);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        _innerHandlerMock.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.IsAny<HttpRequestMessage>(),
            ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task SendAsync_WhenInitializeFails_PropagatesException()
    {
        // Arrange
        var request = new HttpRequestMessage(HttpMethod.Get, "https://api.test.com/test");

        _authServiceMock.Setup(x => x.InitializeAsync())
            .ThrowsAsync(new InvalidOperationException("Init failed"));

        var invoker = new HttpMessageInvoker(_handler);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => invoker.SendAsync(request, CancellationToken.None));
    }

    [Fact]
    public async Task SendAsync_WhenSetClientCookieFails_PropagatesException()
    {
        // Arrange
        var request = new HttpRequestMessage(HttpMethod.Get, "https://api.test.com/test");

        _authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);
        _authServiceMock.Setup(x => x.SetClientCookieAsync())
            .ThrowsAsync(new HttpRequestException("Cookie failed"));

        var invoker = new HttpMessageInvoker(_handler);

        // Act & Assert
        await Assert.ThrowsAsync<HttpRequestException>(
            () => invoker.SendAsync(request, CancellationToken.None));
    }

    [Fact]
    public async Task SendAsync_ExecutesInCorrectOrder()
    {
        // Arrange
        var request = new HttpRequestMessage(HttpMethod.Get, "https://api.test.com/test");
        var callOrder = new List<string>();

        _authServiceMock.Setup(x => x.InitializeAsync())
            .Callback(() => callOrder.Add("Initialize"))
            .Returns(Task.CompletedTask);

        _authServiceMock.Setup(x => x.SetClientCookieAsync())
            .Callback(() => callOrder.Add("SetCookie"))
            .Returns(Task.CompletedTask);

        _authServiceMock.Setup(x => x.IsAuthenticated).Returns(true);
        _authServiceMock.Setup(x => x.Token).Returns("token");

        SetupInnerHandler(HttpStatusCode.OK, () => callOrder.Add("SendRequest"));

        var invoker = new HttpMessageInvoker(_handler);

        // Act
        await invoker.SendAsync(request, CancellationToken.None);

        // Assert
        Assert.Equal(_expected, callOrder);
    }

    [Theory]
    [InlineData("https://api.test.com/private-messages")]
    [InlineData("https://api.test.com/channels")]
    [InlineData("https://api.test.com/messages")]
    public async Task SendAsync_WorksWithDifferentUrls(string url)
    {
        // Arrange
        var request = new HttpRequestMessage(HttpMethod.Get, url);

        _authServiceMock.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);
        _authServiceMock.Setup(x => x.SetClientCookieAsync()).Returns(Task.CompletedTask);
        _authServiceMock.Setup(x => x.IsAuthenticated).Returns(false);

        SetupInnerHandler(HttpStatusCode.OK);

        var invoker = new HttpMessageInvoker(_handler);

        // Act
        var response = await invoker.SendAsync(request, CancellationToken.None);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private void SetupInnerHandler(HttpStatusCode statusCode, Action? callback = null)
    {
        _innerHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback(() => callback?.Invoke())
            .ReturnsAsync(new HttpResponseMessage(statusCode));
    }
}