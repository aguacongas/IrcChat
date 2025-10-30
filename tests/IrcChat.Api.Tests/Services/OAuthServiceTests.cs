// tests/IrcChat.Api.Tests/Services/OAuthServiceTests.cs
using FluentAssertions;
using IrcChat.Api.Services;
using IrcChat.Shared.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using System.Net;
using System.Text.Json;
using Xunit;

namespace IrcChat.Api.Tests.Services;

public class OAuthServiceTests
{
    private readonly Mock<HttpMessageHandler> _httpMessageHandlerMock;
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly Mock<ILogger<OAuthService>> _loggerMock;
    private readonly OAuthService _oauthService;

    public OAuthServiceTests()
    {
        _httpMessageHandlerMock = new Mock<HttpMessageHandler>();
        _httpClient = new HttpClient(_httpMessageHandlerMock.Object);

        var configData = new Dictionary<string, string>
        {
            { "OAuth:Google:ClientId", "test-client-id" },
            { "OAuth:Google:ClientSecret", "test-client-secret" },
            { "OAuth:Microsoft:ClientId", "test-ms-client-id" },
            { "OAuth:Microsoft:ClientSecret", "test-ms-client-secret" },
            { "OAuth:Facebook:AppId", "test-fb-app-id" },
            { "OAuth:Facebook:AppSecret", "test-fb-app-secret" }
        };

        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData!)
            .Build();

        _loggerMock = new Mock<ILogger<OAuthService>>();
        _oauthService = new OAuthService(_httpClient, _configuration, _loggerMock.Object);
    }

    [Fact]
    public void GetProviderConfig_ForGoogle_ShouldReturnGoogleConfig()
    {
        // Act
        var config = _oauthService.GetProviderConfig(ExternalAuthProvider.Google);

        // Assert
        config.Should().NotBeNull();
        config.ClientId.Should().Be("test-client-id");
        config.ClientSecret.Should().Be("test-client-secret");
        config.AuthorizationEndpoint.Should().Contain("google.com");
        config.TokenEndpoint.Should().Contain("oauth2.googleapis.com");
    }

    [Fact]
    public void GetProviderConfig_ForMicrosoft_ShouldReturnMicrosoftConfig()
    {
        // Act
        var config = _oauthService.GetProviderConfig(ExternalAuthProvider.Microsoft);

        // Assert
        config.Should().NotBeNull();
        config.ClientId.Should().Be("test-ms-client-id");
        config.ClientSecret.Should().Be("test-ms-client-secret");
        config.AuthorizationEndpoint.Should().Contain("microsoftonline.com");
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
            token_type = "Bearer"
        };

        var responseContent = new StringContent(
            JsonSerializer.Serialize(tokenResponse),
            System.Text.Encoding.UTF8,
            "application/json");

        _httpMessageHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = responseContent
            });

        // Act
        var result = await _oauthService.ExchangeCodeForTokenAsync(
            ExternalAuthProvider.Google,
            "test_code",
            "http://localhost/callback",
            "test_verifier");

        // Assert
        result.Should().NotBeNull();
        result!.AccessToken.Should().Be("test_access_token");
        result.RefreshToken.Should().Be("test_refresh_token");
        result.ExpiresIn.Should().Be(3600);
    }

    [Fact]
    public async Task ExchangeCodeForTokenAsync_WithErrorResponse_ShouldReturnNull()
    {
        // Arrange
        _httpMessageHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.BadRequest,
                Content = new StringContent("{\"error\": \"invalid_grant\"}")
            });

        // Act
        var result = await _oauthService.ExchangeCodeForTokenAsync(
            ExternalAuthProvider.Google,
            "invalid_code",
            "http://localhost/callback",
            "test_verifier");

        // Assert
        result.Should().BeNull();
    }
}