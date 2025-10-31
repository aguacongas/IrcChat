// tests/IrcChat.Client.Tests/Components/MessageInputTests.cs
using Bunit;
using FluentAssertions;
using IrcChat.Client.Components;
using Microsoft.AspNetCore.Components;
using Xunit;

namespace IrcChat.Client.Tests.Components;

public class MessageInputTests : TestContext
{
    [Fact]
    public void MessageInput_WhenDisconnected_ShouldDisableInput()
    {
        // Act
        var cut = RenderComponent<MessageInput>(parameters => parameters
            .Add(p => p.IsConnected, false));

        // Assert
        var input = cut.Find("input");
        input.HasAttribute("disabled").Should().BeTrue();
    }

    [Fact]
    public void MessageInput_WhenConnected_ShouldEnableInput()
    {
        // Act
        var cut = RenderComponent<MessageInput>(parameters => parameters
            .Add(p => p.IsConnected, true));

        // Assert
        var input = cut.Find("input");
        input.HasAttribute("disabled").Should().BeFalse();
    }

    [Fact]
    public async Task MessageInput_OnSendClick_ShouldTriggerEvent()
    {
        // Arrange
        string? sentMessage = null;
        var cut = RenderComponent<MessageInput>(parameters => parameters
            .Add(p => p.IsConnected, true)
            .Add(p => p.OnSendMessage, EventCallback.Factory.Create<string>(
                this, (msg) => sentMessage = msg)));

        var input = cut.Find("input");
        var button = cut.Find("button");

        // Act
        await cut.InvokeAsync(() => input.Change("Test message"));
        await cut.InvokeAsync(() => button.Click());

        // Assert
        sentMessage.Should().Be("Test message");
    }
}