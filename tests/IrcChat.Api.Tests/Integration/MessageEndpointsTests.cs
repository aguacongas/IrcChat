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
    private readonly HttpClient client = factory.CreateClient();

    [Fact]
    public async Task GetMessages_ShouldReturnNonDeletedMessagesForChannel()
    {
        // Arrange
        var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ChatDbContext>();

        var channel = Guid.NewGuid().ToString();
        var userId = Guid.NewGuid().ToString();

        db.Messages.Add(new Message
        {
            UserId = userId,
            Username = "testuser",
            Content = "Test message 1",
            Channel = channel,
            IsDeleted = true,
            Timestamp = DateTime.UtcNow,
        });
        db.Messages.Add(new Message
        {
            UserId = userId,
            Username = "testuser",
            Content = "Test message 2",
            Channel = channel,
            Timestamp = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        // Act
        var response = await client.GetAsync($"/api/messages/{channel}?userId={userId}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var messages = await response.Content.ReadFromJsonAsync<List<Message>>();
        Assert.NotNull(messages);
        Assert.Single(messages);
    }
}