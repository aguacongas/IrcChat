// tests/IrcChat.Api.Tests/Integration/MessageEndpointsTests.cs
using FluentAssertions;
using IrcChat.Shared.Models;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace IrcChat.Api.Tests.Integration;

public class MessageEndpointsTests(ApiWebApplicationFactory factory) 
    : IClassFixture<ApiWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task SendMessage_ShouldCreateMessage()
    {
        // Arrange
        var messageRequest = new SendMessageRequest
        {
            Username = "testuser",
            Content = "Hello, World!",
            Channel = "general"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/messages", messageRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var message = await response.Content.ReadFromJsonAsync<Message>();
        message.Should().NotBeNull();
        message!.Content.Should().Be("Hello, World!");
        message.Username.Should().Be("testuser");
    }

    [Fact]
    public async Task GetMessages_ShouldReturnMessagesForChannel()
    {
        // Arrange
        var channel = "general";
        await _client.PostAsJsonAsync("/api/messages", new SendMessageRequest
        {
            Username = "testuser",
            Content = "Test message 1",
            Channel = channel
        });

        await _client.PostAsJsonAsync("/api/messages", new SendMessageRequest
        {
            Username = "testuser",
            Content = "Test message 2",
            Channel = channel
        });

        // Act
        var response = await _client.GetAsync($"/api/messages/{channel}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var messages = await response.Content.ReadFromJsonAsync<List<Message>>();
        messages.Should().NotBeNull();
        messages.Should().HaveCountGreaterOrEqualTo(2);
    }
}