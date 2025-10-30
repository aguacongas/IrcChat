// tests/IrcChat.Api.Tests/Integration/PrivateMessageEndpointsTests.cs
using FluentAssertions;
using IrcChat.Shared.Models;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace IrcChat.Api.Tests.Integration;

public class PrivateMessageEndpointsTests(ApiWebApplicationFactory factory) 
    : IClassFixture<ApiWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task GetConversations_ShouldReturnEmptyListForNewUser()
    {
        // Act
        var response = await _client.GetAsync("/api/private-messages/conversations/newuser");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var conversations = await response.Content
            .ReadFromJsonAsync<List<PrivateConversation>>();
        conversations.Should().NotBeNull();
        conversations.Should().BeEmpty();
    }

    [Fact]
    public async Task GetPrivateMessages_BetweenUsers_ShouldReturnMessages()
    {
        // Act
        var response = await _client.GetAsync(
            "/api/private-messages/user1/with/user2");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var messages = await response.Content.ReadFromJsonAsync<List<PrivateMessage>>();
        messages.Should().NotBeNull();
    }

    [Fact]
    public async Task GetUnreadCount_ShouldReturnZeroForNewUser()
    {
        // Act
        var response = await _client.GetAsync("/api/private-messages/newuser/unread-count");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<UnreadCountResponse>();
        result.Should().NotBeNull();
        result!.UnreadCount.Should().Be(0);
    }

    private class UnreadCountResponse
    {
        public int UnreadCount { get; set; }
    }
}