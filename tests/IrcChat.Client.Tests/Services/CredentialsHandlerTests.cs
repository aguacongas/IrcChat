// tests/IrcChat.Client.Tests/Services/CredentialsHandlerTests.cs
using System.Net;
using IrcChat.Client.Services;
using Moq.Protected;

namespace IrcChat.Client.Tests.Services;

public class CredentialsHandlerTests
{
    private readonly Mock<IRequestAuthenticationService> requestAuthenticationServiceMock;
    private readonly Mock<HttpMessageHandler> innerHandlerMock;
    private readonly CredentialsHandler handler;

    public CredentialsHandlerTests()
    {
        requestAuthenticationServiceMock = new Mock<IRequestAuthenticationService>();
        innerHandlerMock = new Mock<HttpMessageHandler>();

        handler = new CredentialsHandler(requestAuthenticationServiceMock.Object)
        {
            InnerHandler = innerHandlerMock.Object,
        };
    }

    [Fact]
    public async Task SendAsync_CallsInitializeAsync()
    {
        // Arrange
        var request = new HttpRequestMessage(HttpMethod.Get, "https://api.test.com/test");

        requestAuthenticationServiceMock.Setup(x => x.ConnectionId).Returns((string?)null);

        SetupInnerHandler(HttpStatusCode.OK);

        var invoker = new HttpMessageInvoker(handler);

        // Act
        await invoker.SendAsync(request, CancellationToken.None);

        // Assert
        requestAuthenticationServiceMock.Verify(x => x.ConnectionId, Times.Once);
    }

    [Fact]
    public async Task SendAsync_WhenAuthenticated_AddsBearerToken()
    {
        // Arrange
        var token = "test-jwt-token-123";
        var request = new HttpRequestMessage(HttpMethod.Get, "https://api.test.com/test");

        requestAuthenticationServiceMock.Setup(x => x.Token).Returns(token);
        requestAuthenticationServiceMock.Setup(x => x.ConnectionId).Returns((string?)null);

        SetupInnerHandler(HttpStatusCode.OK);

        var invoker = new HttpMessageInvoker(handler);

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

        requestAuthenticationServiceMock.Setup(x => x.ConnectionId).Returns((string?)null);

        SetupInnerHandler(HttpStatusCode.OK);

        var invoker = new HttpMessageInvoker(handler);

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
        var request = new HttpRequestMessage(
            HttpMethod.Get,
            "https://api.test.com/api/private-messages/user123/unread-count");

        requestAuthenticationServiceMock.Setup(x => x.ConnectionId).Returns(connectionId);

        SetupInnerHandler(HttpStatusCode.OK);

        var invoker = new HttpMessageInvoker(handler);

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
        var request = new HttpRequestMessage(
            HttpMethod.Get,
            "https://api.test.com/api/private-messages/user123/unread-count");

        requestAuthenticationServiceMock.Setup(x => x.Token).Returns(token);
        requestAuthenticationServiceMock.Setup(x => x.ConnectionId).Returns(connectionId);

        SetupInnerHandler(HttpStatusCode.OK);

        var invoker = new HttpMessageInvoker(handler);

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

        requestAuthenticationServiceMock.Setup(x => x.ConnectionId).Returns((string?)null);

        SetupInnerHandler(HttpStatusCode.OK);

        var invoker = new HttpMessageInvoker(handler);

        // Act
        var response = await invoker.SendAsync(request, CancellationToken.None);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        innerHandlerMock.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.IsAny<HttpRequestMessage>(),
            ItExpr.IsAny<CancellationToken>());
    }

    private void SetupInnerHandler(HttpStatusCode statusCode, Action? callback = null)
    {
        innerHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback(() => callback?.Invoke())
            .ReturnsAsync(new HttpResponseMessage(statusCode));
    }
}