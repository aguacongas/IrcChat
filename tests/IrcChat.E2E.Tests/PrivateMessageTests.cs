// tests/IrcChat.E2E.Tests/PrivateMessageTests.cs
using FluentAssertions;
using Microsoft.Playwright;
using Xunit;

namespace IrcChat.E2E.Tests;

public class PrivateMessageTests(PlaywrightSetup setup) : IClassFixture<PlaywrightSetup>
{
    private const string BaseUrl = "https://localhost:7001";

    [Fact]
    public async Task SendPrivateMessage_ShouldOpenChatWindow()
    {
        // Arrange
        var context = await setup.Browser!.NewContextAsync();
        var page1 = await context.NewPageAsync();
        var page2 = await context.NewPageAsync();

        try
        {
            var user1 = "PMTest1";
            var user2 = "PMTest2";
            var channelName = $"pm-test-{Guid.NewGuid():N}";

            // Les deux utilisateurs rejoignent le même canal
            await LoginAsGuest(page1, user1);
            await page1.WaitForSelectorAsync(".chat-container");
            await page1.FillAsync(".channel-input input", channelName);
            await page1.ClickAsync(".channel-input button");
            await page1.WaitForTimeoutAsync(500);
            await page1.ClickAsync($"text=#{channelName}");

            await LoginAsGuest(page2, user2);
            await page2.WaitForSelectorAsync(".chat-container");
            await page2.WaitForTimeoutAsync(1000);
            await page2.ClickAsync($"text=#{channelName}");
            await page2.WaitForTimeoutAsync(1000);

            // Act - User 1 clique sur User 2 dans la liste
            await page1.WaitForSelectorAsync($"text={user2}");
            await page1.ClickAsync($".user-list >> text={user2}");
            await page1.WaitForTimeoutAsync(500);

            // Assert - Une fenêtre de chat privé devrait s'ouvrir
            var privateChatWindow = await page1.Locator(".private-chat-window").IsVisibleAsync();
            privateChatWindow.Should().BeTrue();

            var chatHeader = await page1.TextContentAsync(".private-chat-window .chat-header");
            chatHeader.Should().Contain(user2);
        }
        finally
        {
            await page1.CloseAsync();
            await page2.CloseAsync();
            await context.CloseAsync();
        }
    }

    private static async Task LoginAsGuest(IPage page, string username)
    {
        await page.GotoAsync(BaseUrl);
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await page.FillAsync("input[placeholder*='pseudo']", username);
        await page.WaitForTimeoutAsync(1000);
        
        var guestButton = page.Locator("button:has-text('Entrer en tant qu\\'invité')");
        await guestButton.WaitForAsync(new() { Timeout = 5000 });
        await guestButton.ClickAsync();
        
        await page.WaitForURLAsync("**/chat", new() { Timeout = 5000 });
    }
}