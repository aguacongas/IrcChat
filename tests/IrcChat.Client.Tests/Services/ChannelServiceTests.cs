using Xunit;
using FluentAssertions;
using IrcChat.Client.Services;

namespace IrcChat.Client.Tests.Services;

public class ChannelServiceTests
{
    [Fact]
    public void ChannelService_ShouldInitializeWithNoChannels()
    {
        var service = new ChannelService();
        service.Channels.Should().BeEmpty();
    }

    [Fact]
    public void AddChannel_ShouldAddToList()
    {
        var service = new ChannelService();
        service.AddChannel("general");
        service.Channels.Should().Contain("general");
    }

    [Fact]
    public void RemoveChannel_ShouldRemoveFromList()
    {
        var service = new ChannelService();
        service.AddChannel("general");
        service.RemoveChannel("general");
        service.Channels.Should().NotContain("general");
    }
}