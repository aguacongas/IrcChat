using Xunit;
using FluentAssertions;
using IrcChat.Client.Services;
using IrcChat.Shared.Models;
using System.Net.Http;

namespace IrcChat.Client.Tests.Services;

public class PrivateMessageServiceTests
{
    [Fact]
    public void SendPrivateMessage_ShouldStoreMessage()
    {
        var service = new PrivateMessageService(new HttpClient());
        var msg = new Message
        {
            Id = Guid.NewGuid(),
            Username = "user1",
            Content = "hello",
            Channel = "pm-user2",
            Timestamp = DateTime.UtcNow
        };
        service.SendPrivateMessage("user2", msg);
        var list = service.GetMessages("user2");
        list.Should().ContainEquivalentOf(msg);
    }

    [Fact]
    public void GetMessages_ShouldReturnEmptyForUnknownUser()
    {
        var service = new PrivateMessageService(new HttpClient());
        service.GetMessages("unknown").Should().BeEmpty();
    }
}