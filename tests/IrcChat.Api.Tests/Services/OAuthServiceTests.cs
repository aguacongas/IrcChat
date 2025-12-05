// tests/IrcChat.Api.Tests/Services/OAuthServiceTests.cs
using System.Net;
using System.Text.Json;
using IrcChat.Api.Services;
using IrcChat.Shared.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using Xunit;

namespace IrcChat.Api.Tests.Services;

public class OAuthServiceTests
{
    private readonly Mock<HttpMessageHandler> httpMessageHandlerMock;
    private readonly HttpClient httpClient;
    private readonly IConfiguration configuration;
    private readonly Mock<ILogger<OAuthService>> loggerMock;
    private readonly OAuthService oauthService;

    public OAuthServiceTests()
    {
        httpMessageHandlerMock = new Mock<HttpMessageHandler>();
        httpClient = new HttpClient(httpMessageHandlerMock.Object);

        var configData = new Dictionary<string, string>
        {
            { "OAuth:Google:ClientId", "test-client-id" },
            { "OAuth:Google:ClientSecret", "test-client-secret" },
            { "OAuth:Microsoft:ClientId", "test-ms-client-id" },
            { "OAuth:Microsoft:ClientSecret", "test-ms-client-secret" },
            { "OAuth:Facebook:AppId", "test-fb-app-id" },
            { "OAuth:Facebook:AppSecret", "test-fb-app-secret" },
        };

        configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData!)
            .Build();

        loggerMock = new Mock<ILogger<OAuthService>>();
        oauthService = new OAuthService(httpClient, configuration, loggerMock.Object);
    }

    [Fact]
    public void GetProviderConfig_ForGoogle_ShouldReturnGoogleConfig()
    {
        // Act
        var config = oauthService.GetProviderConfig(ExternalAuthProvider.Google);

        // Assert
        Assert.NotNull(config);
        Assert.Equal("test-client-id", config.ClientId);
        Assert.Equal("test-client-secret", config.ClientSecret);
        Assert.Contains("google.com", config.AuthorizationEndpoint);
        Assert.Contains("oauth2.googleapis.com", config.TokenEndpoint);
    }

    [Fact]
    public void GetProviderConfig_ForMicrosoft_ShouldReturnMicrosoftConfig()
    {
        // Act
        var config = oauthService.GetProviderConfig(ExternalAuthProvider.Microsoft);

        // Assert
        Assert.NotNull(config);
        Assert.Equal("test-ms-client-id", config.ClientId);
        Assert.Equal("test-ms-client-secret", config.ClientSecret);
        Assert.Contains("microsoftonline.com", config.AuthorizationEndpoint);
    }

    [Fact]
    public async Task ExchangeCodeForTokenAsync_WithValidResponse_ShouldReturnToken()
    {
        // Arrange
        var tokenResponse = new
        {
            access_token = "test_access_token",
            refresh_token = "test_refresh_token",
            expires_in = 3600,
            token_type = "Bearer",
        };

        var responseContent = new StringContent(
            JsonSerializer.Serialize(tokenResponse),
            System.Text.Encoding.UTF8,
            "application/json");

        httpMessageHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = responseContent,
            });

        // Act
        var result = await oauthService.ExchangeCodeForTokenAsync(
            ExternalAuthProvider.Google,
            "test_code",
            "http://localhost/callback",
            "test_verifier");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("test_access_token", result!.AccessToken);
        Assert.Equal("test_refresh_token", result.RefreshToken);
        Assert.Equal(3600, result.ExpiresIn);
    }

    [Fact]
    public async Task ExchangeCodeForTokenAsync_WithErrorResponse_ShouldReturnNull()
    {
        // Arrange
        httpMessageHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.BadRequest,
                Content = new StringContent("{\"error\": \"invalid_grant\"}"),
            });

        // Act
        var result = await oauthService.ExchangeCodeForTokenAsync(
            ExternalAuthProvider.Google,
            "invalid_code",
            "http://localhost/callback",
            "test_verifier");

        // Assert
        Assert.Null(result);
    }
}