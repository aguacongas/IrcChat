// tests/IrcChat.Api.Tests/Integration/ChannelEndpointsTests.cs
using FluentAssertions;
using IrcChat.Shared.Models;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Xunit;

namespace IrcChat.Api.Tests.Integration;

public class ChannelEndpointsTests(ApiWebApplicationFactory factory) 
    : IClassFixture<ApiWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task GetChannels_ShouldReturnChannelList()
    {
        // Act
        var response = await _client.GetAsync("/api/channels");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var channels = await response.Content.ReadFromJsonAsync<List<Channel>>();
        channels.Should().NotBeNull();
    }

    [Fact]
    public async Task CreateChannel_WithAuthentication_ShouldCreateChannel()
    {
        // Arrange
        var token = await GetAuthTokenAsync();
        _client.DefaultRequestHeaders.Authorization = 
            new AuthenticationHeaderValue("Bearer", token);

        var channel = new Channel
        {
            Name = "test-channel",
            CreatedBy = "testuser"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/channels", channel);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await response.Content.ReadFromJsonAsync<Channel>();
        created.Should().NotBeNull();
        created!.Name.Should().Be("test-channel");
    }

    [Fact]
    public async Task CreateChannel_WithoutAuthentication_ShouldReturnForbidden()
    {
        // Arrange
        var channel = new Channel
        {
            Name = "test-channel",
            CreatedBy = "testuser"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/channels", channel);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private async Task<string> GetAuthTokenAsync()
    {
        var loginRequest = new LoginRequest
        {
            Username = "admin",
            Password = "admin123"
        };

        var response = await _client.PostAsJsonAsync("/api/auth/login", loginRequest);
        var result = await response.Content.ReadFromJsonAsync<LoginResponse>();
        return result!.Token;
    }
}