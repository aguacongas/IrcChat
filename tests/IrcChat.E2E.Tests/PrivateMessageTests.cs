using FluentAssertions;
using IrcChat.E2E.Tests.Helpers;
using Microsoft.Playwright;
using Xunit;

namespace IrcChat.E2E.Tests;

public class PrivateMessageTests(PlaywrightSetup setup) : IClassFixture<PlaywrightSetup>
{
    private const string BaseUrl = "https://localhost:7001";

    [Fact]
    public async Task SendPrivateMessage_ShouldOpenChatWindow()
    {
        var context = await setup.Browser!.NewContextAsync();
        var page1 = await PlaywrightHelpers.CreateAuthenticatedPage(setup.Browser, "PMTest1");
        var page2 = await PlaywrightHelpers.CreateAuthenticatedPage(setup.Browser, "PMTest2");

        try
        {
            var channelName = $"pm-test-{Guid.NewGuid():N}";

            // Les deux utilisateurs rejoignent le même canal
            await PlaywrightHelpers.CreateAndJoinChannel(page1, channelName);

            await page2.WaitForSelectorAsync(".chat-container");
            await page2.ClickAsync($"text=#{channelName}");
            await page2.WaitForTimeoutAsync(1000);

            // Act - User 1 clique sur User 2 dans la liste
            await page1.WaitForSelectorAsync($"text=PMTest2", new() { Timeout = 10000 });
            await page1.ClickAsync($".user-list >> text=PMTest2");
            await page1.WaitForTimeoutAsync(500);

            // Assert - Une fenêtre de chat privé devrait s'ouvrir
            var privateChatWindow = await page1.Locator(".private-chat-window").IsVisibleAsync();
            privateChatWindow.Should().BeTrue();

            var chatHeader = await page1.TextContentAsync(".private-chat-window .chat-header");
            chatHeader.Should().Contain("PMTest2");
        }
        finally
        {
            await page1.CloseAsync();
            await page2.CloseAsync();
            await context.CloseAsync();
        }
    }
}