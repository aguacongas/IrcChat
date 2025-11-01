using Bunit;
using Xunit;
using FluentAssertions;
using IrcChat.Client.Components;

namespace IrcChat.Client.Tests.Components;

public class ChannelListTests : TestContext
{
    [Fact]
    public void RenderEmpty_ShouldShowNoChannels()
    {
        var cut = RenderComponent<ChannelList>(p => p.Add(c => c.Channels, new List<string>()));
        cut.FindAll(".channel").Should().BeEmpty();
    }

    [Fact]
    public void RenderWithChannels_ShouldShowAllChannels()
    {
        var list = new List<string> { "general", "random" };
        var cut = RenderComponent<ChannelList>(p => p.Add(c => c.Channels, list));
        cut.FindAll(".channel").Should().HaveCount(2);
    }
}