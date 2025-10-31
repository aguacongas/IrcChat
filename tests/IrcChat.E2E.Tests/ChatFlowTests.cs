// tests/IrcChat.E2E.Tests/ChatFlowTests.cs
using FluentAssertions;
using Microsoft.Playwright;
using Xunit;

namespace IrcChat.E2E.Tests;

public class ChatFlowTests(PlaywrightSetup setup) : IClassFixture<PlaywrightSetup>
{
    private static readonly string _baseUrl = "https://localhost:7001";

    [Fact]
    public async Task SendMessage_ShouldAppearInChatWindow()
    {
        // Arrange
        var page = await setup.Browser!.NewPageAsync();

        try
        {
            // Se connecter en tant qu'invité
            await LoginAsGuest(page, "E2ETestUser1");

            // Attendre que la page de chat soit chargée
            await page.WaitForSelectorAsync(".chat-container");

            // Rejoindre ou créer un canal de test
            var channelName = $"e2e-test-{Guid.NewGuid():N}";
            await page.FillAsync(".channel-input input", channelName);
            await page.ClickAsync(".channel-input button");
            await page.WaitForTimeoutAsync(500);

            // Cliquer sur le canal créé
            await page.ClickAsync($"text=#{channelName}");
            await page.WaitForTimeoutAsync(500);

            // Act - Envoyer un message
            var testMessage = $"Test message {DateTime.UtcNow:HH:mm:ss}";
            await page.FillAsync(".input-area input", testMessage);
            await page.ClickAsync(".input-area button:has-text('Envoyer')");

            // Assert
            await page.WaitForSelectorAsync($"text={testMessage}");
            var messages = await page.Locator(".message .content").AllTextContentsAsync();
            messages.Should().Contain(testMessage);
        }
        finally
        {
            await page.CloseAsync();
        }
    }

    [Fact]
    public async Task MultipleUsers_ShouldSeeEachOthersMessages()
    {
        // Arrange
        var context = await setup.Browser!.NewContextAsync();
        var page1 = await context.NewPageAsync();
        var page2 = await context.NewPageAsync();

        try
        {
            var channelName = $"e2e-multi-{Guid.NewGuid():N}";
            var user1 = "E2EUser1";
            var user2 = "E2EUser2";

            // User 1 se connecte et crée un canal
            await LoginAsGuest(page1, user1);
            await page1.WaitForSelectorAsync(".chat-container");
            await page1.FillAsync(".channel-input input", channelName);
            await page1.ClickAsync(".channel-input button");
            await page1.WaitForTimeoutAsync(500);
            await page1.ClickAsync($"text=#{channelName}");

            // User 2 se connecte et rejoint le même canal
            await LoginAsGuest(page2, user2);
            await page2.WaitForSelectorAsync(".chat-container");
            await page2.WaitForTimeoutAsync(1000);
            await page2.ClickAsync($"text=#{channelName}");
            await page2.WaitForTimeoutAsync(500);

            // Act - User 1 envoie un message
            var message1 = $"Hello from {user1}";
            await page1.FillAsync(".input-area input", message1);
            await page1.ClickAsync(".input-area button:has-text('Envoyer')");

            // Assert - User 2 devrait voir le message
            await page2.WaitForSelectorAsync($"text={message1}", new() { Timeout = 5000 });
            var user2Messages = await page2.Locator(".message .content").AllTextContentsAsync();
            user2Messages.Should().Contain(message1);

            // Act - User 2 répond
            var message2 = $"Hello back from {user2}";
            await page2.FillAsync(".input-area input", message2);
            await page2.ClickAsync(".input-area button:has-text('Envoyer')");

            // Assert - User 1 devrait voir la réponse
            await page1.WaitForSelectorAsync($"text={message2}", new() { Timeout = 5000 });
            var user1Messages = await page1.Locator(".message .content").AllTextContentsAsync();
            user1Messages.Should().Contain(message2);
        }
        finally
        {
            await page1.CloseAsync();
            await page2.CloseAsync();
            await context.CloseAsync();
        }
    }

    [Fact]
    public async Task CreateChannel_ShouldAppearInChannelList()
    {
        // Arrange
        var page = await setup.Browser!.NewPageAsync();

        try
        {
            await LoginAsGuest(page, "E2EChannelCreator");
            await page.WaitForSelectorAsync(".chat-container");

            // Act
            var channelName = $"new-channel-{Guid.NewGuid():N}";
            await page.FillAsync(".channel-input input", channelName);
            await page.ClickAsync(".channel-input button");
            await page.WaitForTimeoutAsync(1000);

            // Assert
            var channelExists = await page.Locator($"text=#{channelName}").IsVisibleAsync();
            channelExists.Should().BeTrue();
        }
        finally
        {
            await page.CloseAsync();
        }
    }

    [Fact]
    public async Task UserList_ShouldShowConnectedUsers()
    {
        // Arrange
        var context = await setup.Browser!.NewContextAsync();
        var page1 = await context.NewPageAsync();
        var page2 = await context.NewPageAsync();

        try
        {
            var channelName = $"userlist-test-{Guid.NewGuid():N}";
            var user1 = "UserListTest1";
            var user2 = "UserListTest2";

            // User 1 crée et rejoint le canal
            await LoginAsGuest(page1, user1);
            await page1.WaitForSelectorAsync(".chat-container");
            await page1.FillAsync(".channel-input input", channelName);
            await page1.ClickAsync(".channel-input button");
            await page1.WaitForTimeoutAsync(500);
            await page1.ClickAsync($"text=#{channelName}");

            // Act - User 2 rejoint
            await LoginAsGuest(page2, user2);
            await page2.WaitForSelectorAsync(".chat-container");
            await page2.WaitForTimeoutAsync(1000);
            await page2.ClickAsync($"text=#{channelName}");
            await page2.WaitForTimeoutAsync(1000);

            // Assert - Les deux utilisateurs devraient voir l'autre dans la liste
            await page1.WaitForSelectorAsync($"text={user2}", new() { Timeout = 5000 });
            var user1List = await page1.Locator(".user-list li").AllTextContentsAsync();
            user1List.Should().Contain(u => u.Contains(user2));

            await page2.WaitForSelectorAsync($"text={user1}", new() { Timeout = 5000 });
            var user2List = await page2.Locator(".user-list li").AllTextContentsAsync();
            user2List.Should().Contain(u => u.Contains(user1));
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
        await page.GotoAsync(_baseUrl);
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await page.FillAsync("input[placeholder*='pseudo']", username);
        await page.WaitForTimeoutAsync(1000);
        
        var guestButton = page.Locator("button:has-text('Entrer en tant qu\\'invité')");
        await guestButton.WaitForAsync(new() { Timeout = 5000 });
        await guestButton.ClickAsync();
        
        await page.WaitForURLAsync("**/chat", new() { Timeout = 5000 });
    }
}