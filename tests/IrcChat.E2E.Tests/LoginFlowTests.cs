// tests/IrcChat.E2E.Tests/LoginFlowTests.cs
using FluentAssertions;
using Microsoft.Playwright;
using Xunit;

namespace IrcChat.E2E.Tests;

public class LoginFlowTests(PlaywrightSetup setup) : IClassFixture<PlaywrightSetup>
{
    private const string BaseUrl = "https://localhost:7001";

    [Fact]
    public async Task GuestLogin_ShouldNavigateToChatPage()
    {
        // Arrange
        var page = await setup.Browser!.NewPageAsync();

        try
        {
            // Act
            await page.GotoAsync(BaseUrl);
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

            // Entrer un nom d'utilisateur
            await page.FillAsync("input[placeholder*='pseudo']", "E2ETestUser");
            await page.WaitForTimeoutAsync(1000); // Attendre le debounce

            // Cliquer sur "Entrer en tant qu'invité"
            var guestButton = page.Locator("button:has-text('Entrer en tant qu\\'invité')");
            await guestButton.WaitForAsync();
            await guestButton.ClickAsync();

            // Assert
            await page.WaitForURLAsync("**/chat");
            var url = page.Url;
            url.Should().Contain("/chat");

            // Vérifier que le nom d'utilisateur est affiché
            var userInfo = await page.TextContentAsync(".user-info");
            userInfo.Should().Contain("E2ETestUser");
        }
        finally
        {
            await page.CloseAsync();
        }
    }

    [Fact]
    public async Task ReservedUsername_ShouldShowLoginOptions()
    {
        // Arrange
        var page = await setup.Browser!.NewPageAsync();

        try
        {
            // Act
            await page.GotoAsync(BaseUrl);
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

            // Entrer un pseudo qui pourrait être réservé
            await page.FillAsync("input[placeholder*='pseudo']", "testuser");
            await page.WaitForTimeoutAsync(1500); // Attendre le debounce et la vérification

            // Assert
            var pageContent = await page.TextContentAsync("body");
            
            // Si le pseudo est disponible
            if (pageContent.Contains("Entrer en tant qu'invité"))
            {
                var reserveButton = page.Locator("button:has-text('Réserver')");
                await reserveButton.WaitForAsync(new() { Timeout = 3000 });
                (await reserveButton.IsVisibleAsync()).Should().BeTrue();
            }
            // Si le pseudo est réservé
            else if (pageContent.Contains("réservé"))
            {
                var loginButton = page.Locator("button:has-text('Se connecter')");
                await loginButton.WaitForAsync(new() { Timeout = 3000 });
                (await loginButton.IsVisibleAsync()).Should().BeTrue();
            }
        }
        finally
        {
            await page.CloseAsync();
        }
    }
}