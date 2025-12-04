// tests/IrcChat.Client.Tests/Services/CredentialsHandlerTests.cs
using System.Net;
using IrcChat.Client.Services;
using Moq;
using Moq.Protected;
using Xunit;

namespace IrcChat.Client.Tests.Services;

public class CredentialsHandlerTests
{
    private readonly Mock<IRequestAuthenticationService> _requestAuthenticationServiceMock;
    private readonly Mock<HttpMessageHandler> _innerHandlerMock;
    private readonly CredentialsHandler _handler;

    public CredentialsHandlerTests()
    {
        _requestAuthenticationServiceMock = new Mock<IRequestAuthenticationService>();
        _innerHandlerMock = new Mock<HttpMessageHandler>();

        _handler = new CredentialsHandler(_requestAuthenticationServiceMock.Object)
        {
            InnerHandler = _innerHandlerMock.Object
        };
    }

    [Fact]
    public async Task SendAsync_CallsInitializeAsync()
    {
        // Arrange
        var request = new HttpRequestMessage(HttpMethod.Get, "https://api.test.com/test");

        _requestAuthenticationServiceMock.Setup(x => x.ConnectionId).Returns((string?)null);

        SetupInnerHandler(HttpStatusCode.OK);

        var invoker = new HttpMessageInvoker(_handler);

        // Act
        await invoker.SendAsync(request, CancellationToken.None);

        // Assert
        _requestAuthenticationServiceMock.Verify(x => x.ConnectionId, Times.Once);
    }

    [Fact]
    public async Task SendAsync_WhenAuthenticated_AddsBearerToken()
    {
        // Arrange
        var token = "test-jwt-token-123";
        var request = new HttpRequestMessage(HttpMethod.Get, "https://api.test.com/test");

        _requestAuthenticationServiceMock.Setup(x => x.Token).Returns(token);
        _requestAuthenticationServiceMock.Setup(x => x.ConnectionId).Returns((string?)null);

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

        _requestAuthenticationServiceMock.Setup(x => x.ConnectionId).Returns((string?)null);

        SetupInnerHandler(HttpStatusCode.OK);

        var invoker = new HttpMessageInvoker(_handler);

        // Act
        await invoker.SendAsync(request, CancellationToken.None);

        // Assert
        Assert.Null(request.Headers.Authorization);
    }

    [Fact]
    public async Task SendAsync_ForPrivateMessages_AddsConnectionIdHeader()
    {
        // Arrange
        var connectionId = "test-connection-123";
        var request = new HttpRequestMessage(HttpMethod.Get,
            "https://api.test.com/api/private-messages/user123/unread-count");

        _requestAuthenticationServiceMock.Setup(x => x.ConnectionId).Returns(connectionId);

        SetupInnerHandler(HttpStatusCode.OK);

        var invoker = new HttpMessageInvoker(_handler);

        // Act
        await invoker.SendAsync(request, CancellationToken.None);

        // Assert
        Assert.True(request.Headers.Contains("X-ConnectionId"));
        Assert.Equal(connectionId, request.Headers.GetValues("X-ConnectionId").First());
    }

    [Fact]
    public async Task SendAsync_WithBothTokenAndConnectionId_AddsBoth()
    {
        // Arrange
        var token = "test-token";
        var connectionId = "test-connection";
        var request = new HttpRequestMessage(HttpMethod.Get,
            "https://api.test.com/api/private-messages/user123/unread-count");

        _requestAuthenticationServiceMock.Setup(x => x.Token).Returns(token);
        _requestAuthenticationServiceMock.Setup(x => x.ConnectionId).Returns(connectionId);

        SetupInnerHandler(HttpStatusCode.OK);

        var invoker = new HttpMessageInvoker(_handler);

        // Act
        await invoker.SendAsync(request, CancellationToken.None);

        // Assert
        Assert.NotNull(request.Headers.Authorization);
        Assert.Equal(token, request.Headers.Authorization.Parameter);
        Assert.True(request.Headers.Contains("X-ConnectionId"));
        Assert.Equal(connectionId, request.Headers.GetValues("X-ConnectionId").First());
    }

    [Fact]
    public async Task SendAsync_CallsInnerHandler()
    {
        // Arrange
        var request = new HttpRequestMessage(HttpMethod.Get, "https://api.test.com/test");

        _requestAuthenticationServiceMock.Setup(x => x.ConnectionId).Returns((string?)null);

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