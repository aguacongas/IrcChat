using Microsoft.Playwright;
using System.Text.Json;

namespace IrcChat.E2E.Tests.Helpers;

public static class PlaywrightHelpers
{
    public static async Task<IPage> CreateAuthenticatedPage(
        IBrowser browser,
        string username = "TestUser",
        string token = "test-token",
        string email = "test@example.com",
        string baseUrl = "https://localhost:7001")
    {
        var page = await browser.NewPageAsync();

        await page.GotoAsync(baseUrl);
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Entrer le nom d'utilisateur
        await page.FillAsync("input[placeholder*='pseudo']", username);
        await page.WaitForTimeoutAsync(1000);

        var authData = new
        {
            Username = username,
            Token = token,
            IsReserved = true,
            ReservedProvider = 1,
            Email = email,
            AvatarUrl = "https://example.com/avatar.jpg",
            UserId = Guid.NewGuid(),
            IsAdmin = false
        };
        var json = JsonSerializer.Serialize(authData);

        await page.EvaluateAsync(@"(data) => {
            localStorage.setItem('ircchat_unified_auth', data);
        }", json);

        await page.GotoAsync($"{baseUrl}/chat");
        await page.WaitForSelectorAsync(".chat-container", new() { Timeout = 10000 });

        return page;
    }

    /// <summary>
    /// Crée un salon depuis la page paramètres et revient au chat, puis rejoint ce salon.
    /// </summary>
    public static async Task<string> CreateAndJoinChannel(
        IPage page,
        string channelName)
    {
        // Aller dans la page des paramètres via .user-info
        await page.ClickAsync(".user-info");
        await page.WaitForSelectorAsync(".settings-container", new() { Timeout = 5000 });

        await page.FillAsync(".settings-container .input-group input", channelName);
        await page.WaitForTimeoutAsync(1000);

        await page.ClickAsync(".settings-container .input-group .btn-primary");
        await page.WaitForTimeoutAsync(1000);

        var content = await page.ContentAsync();

        await page.WaitForSelectorAsync(".chat-container", new() { Timeout = 5000 });
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

    public static async Task<IReadOnlyList<string>> GetAllMessages(IPage page) => await page.Locator(".message .content").AllTextContentsAsync();

    public static async Task<IReadOnlyList<string>> GetUserList(IPage page) => await page.Locator(".user-list li").AllTextContentsAsync();
}