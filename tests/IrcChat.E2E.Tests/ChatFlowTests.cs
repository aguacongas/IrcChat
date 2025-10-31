using FluentAssertions;
using IrcChat.E2E.Tests.Helpers;
using Microsoft.Playwright;
using Xunit;

namespace IrcChat.E2E.Tests;

public class ChatFlowTests(PlaywrightSetup setup) : IClassFixture<PlaywrightSetup>
{
    private static readonly string _baseUrl = "https://localhost:7001";

    [Fact]
    public async Task SendMessage_ShouldAppearInChatWindow()
    {
        var page = await PlaywrightHelpers.CreateAuthenticatedPage(setup.Browser!, "E2ETestUser1");
        try
        {
            var channelName = $"e2e-test-{Guid.NewGuid():N}";
            await PlaywrightHelpers.CreateAndJoinChannel(page, channelName);

            // Act - Envoyer un message
            var testMessage = $"Test message {DateTime.UtcNow:HH:mm:ss}";
            await PlaywrightHelpers.SendMessage(page, testMessage);

            // Assert
            await page.WaitForSelectorAsync($"text={testMessage}", new() { Timeout = 5000 });
            var messages = await PlaywrightHelpers.GetAllMessages(page);
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
        var context = await setup.Browser!.NewContextAsync();
        var page1 = await PlaywrightHelpers.CreateAuthenticatedPage(setup.Browser, "E2EUser1");
        var page2 = await PlaywrightHelpers.CreateAuthenticatedPage(setup.Browser, "E2EUser2");

        try
        {
            var channelName = $"e2e-multi-{Guid.NewGuid():N}";

            // User 1 crée et rejoint le canal
            await PlaywrightHelpers.CreateAndJoinChannel(page1, channelName);

            // User 2 rejoint le canal
            await page2.WaitForSelectorAsync(".chat-container");
            await page2.ClickAsync($"text=#{channelName}");
            await page2.WaitForTimeoutAsync(500);

            // Act - User 1 envoie un message
            var message1 = $"Hello from E2EUser1";
            await PlaywrightHelpers.SendMessage(page1, message1);

            // Assert - User 2 devrait voir le message
            await page2.WaitForSelectorAsync($"text={message1}", new() { Timeout = 5000 });
            var user2Messages = await PlaywrightHelpers.GetAllMessages(page2);
            user2Messages.Should().Contain(message1);

            // Act - User 2 répond
            var message2 = $"Hello back from E2EUser2";
            await PlaywrightHelpers.SendMessage(page2, message2);

            // Assert - User 1 devrait voir la réponse
            await page1.WaitForSelectorAsync($"text={message2}", new() { Timeout = 5000 });
            var user1Messages = await PlaywrightHelpers.GetAllMessages(page1);
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
        var page = await PlaywrightHelpers.CreateAuthenticatedPage(setup.Browser!, "E2EChannelCreator");
        try
        {
            var channelName = $"new-channel-{Guid.NewGuid():N}";
            await PlaywrightHelpers.CreateAndJoinChannel(page, channelName);

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
        var context = await setup.Browser!.NewContextAsync();
        var page1 = await PlaywrightHelpers.CreateAuthenticatedPage(setup.Browser, "UserListTest1");
        var page2 = await PlaywrightHelpers.CreateAuthenticatedPage(setup.Browser, "UserListTest2");

        try
        {
            var channelName = $"userlist-test-{Guid.NewGuid():N}";

            // User 1 crée et rejoint le canal
            await PlaywrightHelpers.CreateAndJoinChannel(page1, channelName);

            // User 2 rejoint le canal
            await page2.WaitForSelectorAsync(".chat-container");
            await page2.ClickAsync($"text=#{channelName}");
            await page2.WaitForTimeoutAsync(1000);

            // Assert - Les deux utilisateurs devraient voir l'autre dans la liste
            await page1.WaitForSelectorAsync($"text=UserListTest2", new() { Timeout = 5000 });
            var user1List = await PlaywrightHelpers.GetUserList(page1);
            user1List.Should().Contain(u => u.Contains("UserListTest2"));

            await page2.WaitForSelectorAsync($"text=UserListTest1", new() { Timeout = 5000 });
            var user2List = await PlaywrightHelpers.GetUserList(page2);
            user2List.Should().Contain(u => u.Contains("UserListTest1"));
        }
        finally
        {
            await page1.CloseAsync();
            await page2.CloseAsync();
            await context.CloseAsync();
        }
    }
}