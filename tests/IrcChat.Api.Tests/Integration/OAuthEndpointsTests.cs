using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using IrcChat.Api.Data;
using IrcChat.Shared.Models;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace IrcChat.Api.Tests.Integration;

public class OAuthEndpointsTests(ApiWebApplicationFactory factory)
    : IClassFixture<ApiWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient();
    private readonly ApiWebApplicationFactory _factory = factory;

    [Fact]
    public async Task CheckUsername_WithAvailableUsername_ShouldReturnAvailable()
    {
        // Arrange
        var request = new UsernameCheckRequest
        {
            Username = "available_user"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/oauth/check-username", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<UsernameCheckResponse>();
        result.Should().NotBeNull();
        result!.Available.Should().BeTrue();
        result.IsReserved.Should().BeFalse();
    }

    [Fact]
    public async Task CheckUsername_WithReservedUsername_ShouldReturnNotAvailable()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ChatDbContext>();

        var reservedUser = new ReservedUsername
        {
            Id = Guid.NewGuid(),
            Username = "reserved_user",
            Provider = ExternalAuthProvider.Google,
            ExternalUserId = "google-123",
            Email = "reserved@example.com",
            CreatedAt = DateTime.UtcNow,
            LastLoginAt = DateTime.UtcNow,
            IsAdmin = false
        };

        db.ReservedUsernames.Add(reservedUser);
        await db.SaveChangesAsync();

        var request = new UsernameCheckRequest
        {
            Username = "reserved_user"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/oauth/check-username", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<UsernameCheckResponse>();
        result.Should().NotBeNull();
        result!.Available.Should().BeFalse();
        result.IsReserved.Should().BeTrue();
        result.ReservedProvider.Should().Be(ExternalAuthProvider.Google);
    }

    [Fact]
    public async Task GetProviderConfig_ForGoogle_ShouldReturnConfig()
    {
        // Act
        var response = await _client.GetAsync("/api/oauth/config/Google");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var config = await response.Content.ReadFromJsonAsync<ProviderConfigResponse>();
        config.Should().NotBeNull();
        config!.Provider.Should().Be(ExternalAuthProvider.Google);
        config.AuthorizationEndpoint.Should().NotBeNullOrEmpty();
        config.ClientId.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task GetProviderConfig_ForMicrosoft_ShouldReturnConfig()
    {
        // Act
        var response = await _client.GetAsync("/api/oauth/config/Microsoft");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var config = await response.Content.ReadFromJsonAsync<ProviderConfigResponse>();
        config.Should().NotBeNull();
        config!.Provider.Should().Be(ExternalAuthProvider.Microsoft);
    }

    [Fact]
    public async Task GetProviderConfig_ForFacebook_ShouldReturnConfig()
    {
        // Act
        var response = await _client.GetAsync("/api/oauth/config/Facebook");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var config = await response.Content.ReadFromJsonAsync<ProviderConfigResponse>();
        config.Should().NotBeNull();
        config!.Provider.Should().Be(ExternalAuthProvider.Facebook);
    }

    private class ProviderConfigResponse
    {
        public ExternalAuthProvider Provider { get; set; }
        public string AuthorizationEndpoint { get; set; } = string.Empty;
        public string ClientId { get; set; } = string.Empty;
        public string Scope { get; set; } = string.Empty;
    }
}