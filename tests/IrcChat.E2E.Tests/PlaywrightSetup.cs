// tests/IrcChat.E2E.Tests/PlaywrightSetup.cs
using Microsoft.Playwright;
using Xunit;

namespace IrcChat.E2E.Tests;

public class PlaywrightSetup : IAsyncLifetime
{
    public IPlaywright? Playwright { get; private set; }
    public IBrowser? Browser { get; private set; }

    public async Task InitializeAsync()
    {
        // Installation: dotnet tool install --global Microsoft.Playwright.CLI
        // Puis: playwright install

        Playwright = await Microsoft.Playwright.Playwright.CreateAsync();
        Browser = await Playwright.Chromium.LaunchAsync(new()
        {
            Headless = true, // Mettre à false pour voir le navigateur
            SlowMo = 50 // Ralentir les actions pour mieux voir
        });
    }

    public async Task DisposeAsync()
    {
        if (Browser != null)
            await Browser.DisposeAsync();

        Playwright?.Dispose();
    }
}