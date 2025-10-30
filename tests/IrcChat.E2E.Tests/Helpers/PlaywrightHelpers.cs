// tests/IrcChat.E2E.Tests/Helpers/PlaywrightHelpers.cs
using Microsoft.Playwright;

namespace IrcChat.E2E.Tests.Helpers;

public static class PlaywrightHelpers
{
    public static async Task<IPage> CreateAuthenticatedPage(
        IBrowser browser, 
        string username,
        string baseUrl = "https://localhost:7001")
    {
        var page = await browser.NewPageAsync();
        
        await page.GotoAsync(baseUrl);
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        
        // Entrer le nom d'utilisateur
        await page.FillAsync("input[placeholder*='pseudo']", username);
        await page.WaitForTimeoutAsync(1000);
        
        // Se connecter en tant qu'invité
        var guestButton = page.Locator("button:has-text('Entrer en tant qu\\'invité')");
        await guestButton.WaitForAsync(new() { Timeout = 5000 });
        await guestButton.ClickAsync();
        
        await page.WaitForURLAsync("**/chat", new() { Timeout = 5000 });
        
        return page;
    }

    public static async Task<string> CreateAndJoinChannel(
        IPage page, 
        string channelName)
    {
        await page.WaitForSelectorAsync(".chat-container");
        await page.FillAsync(".channel-input input", channelName);
        await page.ClickAsync(".channel-input button");
        await page.WaitForTimeoutAsync(500);
        await page.ClickAsync($"text=#{channelName}");
        await page.WaitForTimeoutAsync(500);
        
        return channelName;
    }

    public static async Task SendMessage(
        IPage page, 
        string message)
    {
        await page.FillAsync(".input-area input", message);
        await page.ClickAsync(".input-area button:has-text('Envoyer')");
        await page.WaitForTimeoutAsync(300);
    }

    public static async Task<bool> WaitForMessage(
        IPage page, 
        string messageText, 
        int timeoutMs = 5000)
    {
        try
        {
            await page.WaitForSelectorAsync(
                $"text={messageText}", 
                new() { Timeout = timeoutMs });
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static async Task<IReadOnlyList<string>> GetAllMessages(IPage page)
    {
        return await page.Locator(".message .content").AllTextContentsAsync();
    }

    public static async Task<IReadOnlyList<string>> GetUserList(IPage page)
    {
        return await page.Locator(".user-list li").AllTextContentsAsync();
    }
}