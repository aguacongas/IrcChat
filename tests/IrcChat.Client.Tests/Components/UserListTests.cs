using Bunit;
using Xunit;
using FluentAssertions;
using IrcChat.Client.Components;

namespace IrcChat.Client.Tests.Components;

public class UserListTests : TestContext
{
    [Fact]
    public void RenderEmpty_ShouldShowNoUsers()
    {
        var cut = RenderComponent<UserList>(p => p.Add(c => c.Users, new List<string>()));
        cut.FindAll(".user").Should().BeEmpty();
    }

    [Fact]
    public void RenderWithUsers_ShouldShowAllUsers()
    {
        var list = new List<string> { "user1", "user2" };
        var cut = RenderComponent<UserList>(p => p.Add(c => c.Users, list));
        cut.FindAll(".user").Should().HaveCount(2);
    }
}