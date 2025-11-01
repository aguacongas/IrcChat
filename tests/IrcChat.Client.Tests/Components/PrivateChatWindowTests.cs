using Bunit;
using Xunit;
using FluentAssertions;
using IrcChat.Client.Components;
using IrcChat.Shared.Models;

namespace IrcChat.Client.Tests.Components;

public class PrivateChatWindowTests : TestContext
{
    [Fact]
    public void ShouldRenderHeaderWithUsername()
    {
        var messages = new List<Message> {
            new() { Id = Guid.NewGuid(), Username = "user2", Content = "Hello!", Channel = "pm-user2", Timestamp = DateTime.UtcNow }
        };
        var cut = RenderComponent<PrivateChatWindow>(parameters =>
            parameters.Add(p => p.TargetUsername, "user2")
                      .Add(p => p.Messages, messages));

        cut.Markup.Should().Contain("user2");
    }
}