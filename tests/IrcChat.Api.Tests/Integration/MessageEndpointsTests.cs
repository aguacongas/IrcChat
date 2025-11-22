// tests/IrcChat.Api.Tests/Integration/MessageEndpointsTests.cs
using System.Net;
using System.Net.Http.Json;
using IrcChat.Api.Data;
using IrcChat.Shared.Models;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace IrcChat.Api.Tests.Integration;

public class MessageEndpointsTests(ApiWebApplicationFactory factory)
    : IClassFixture<ApiWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task GetMessages_ShouldReturnMessagesForChannel()
    {
        // Arrange
        var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ChatDbContext>();

        var channel = Guid.NewGuid().ToString();
        db.Messages.Add(new Message
        {
            Username = "testuser",
            Content = "Test message 1",
            Channel = channel,
            Timestamp = DateTime.UtcNow
        });
        db.Messages.Add(new Message
        {
            Username = "testuser",
            Content = "Test message 2",
            Channel = channel,
            Timestamp = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        // Act
        var response = await _client.GetAsync($"/api/messages/{channel}?userId=test");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var messages = await response.Content.ReadFromJsonAsync<List<Message>>();
        Assert.NotNull(messages);
        Assert.Equal(2, messages.Count);
    }
}