// tests/IrcChat.Client.Tests/Components/MessageListTests.cs
using Bunit;
using FluentAssertions;
using IrcChat.Client.Components;
using IrcChat.Shared.Models;
using Xunit;

namespace IrcChat.Client.Tests.Components;

public class MessageListTests : TestContext
{
    [Fact]
    public void MessageList_WithEmptyMessages_ShouldRenderEmpty()
    {
        // Arrange & Act
        var cut = RenderComponent<MessageList>(parameters => parameters
            .Add(p => p.Messages, new List<Message>())
            .Add(p => p.CurrentUsername, "testuser"));

        // Assert
        cut.MarkupMatches("<div class=\"messages\" diff:ignoreAttributes></div>");
    }

    [Fact]
    public void MessageList_WithMessages_ShouldRenderMessages()
    {
        // Arrange
        var messages = new List<Message>
        {
            new()
            {
                Id = Guid.NewGuid(),
                Username = "user1",
                Content = "Hello!",
                Channel = "general",
                Timestamp = DateTime.UtcNow
            },
            new()
            {
                Id = Guid.NewGuid(),
                Username = "user2",
                Content = "Hi there!",
                Channel = "general",
                Timestamp = DateTime.UtcNow
            }
        };

        // Act
        var cut = RenderComponent<MessageList>(parameters => parameters
            .Add(p => p.Messages, messages)
            .Add(p => p.CurrentUsername, "user1"));

        // Assert
        cut.FindAll(".message").Should().HaveCount(2);
        cut.Find(".message.own .content").TextContent.Should().Be("Hello!");
    }

    [Fact]
    public void MessageList_ShouldMarkOwnMessages()
    {
        // Arrange
        var messages = new List<Message>
        {
            new()
            {
                Id = Guid.NewGuid(),
                Username = "currentuser",
                Content = "My message",
                Channel = "general",
                Timestamp = DateTime.UtcNow
            }
        };

        // Act
        var cut = RenderComponent<MessageList>(parameters => parameters
            .Add(p => p.Messages, messages)
            .Add(p => p.CurrentUsername, "currentuser"));

        // Assert
        cut.Find(".message").ClassList.Should().Contain("own");
    }
}