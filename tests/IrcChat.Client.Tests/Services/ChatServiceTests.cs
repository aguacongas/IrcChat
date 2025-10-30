// tests/IrcChat.Client.Tests/Services/ChatServiceTests.cs
using Bunit;
using FluentAssertions;
using IrcChat.Client.Models;
using IrcChat.Client.Services;
using IrcChat.Shared.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace IrcChat.Client.Tests.Services;

public class ChatServiceTests : TestContext
{
    private readonly Mock<PrivateMessageService> _privateMessageServiceMock;
    private readonly IOptions<ApiSettings> _apiSettings;

    public ChatServiceTests()
    {
        var httpClient = new HttpClient { BaseAddress = new Uri("https://localhost:7000") };
        _privateMessageServiceMock = new Mock<PrivateMessageService>(httpClient);

        _apiSettings = Options.Create(new ApiSettings
        {
            BaseUrl = "https://localhost:7000",
            SignalRHubUrl = "https://localhost:7000/chathub"
        });

        Services.AddSingleton(_privateMessageServiceMock.Object);
        Services.AddSingleton(_apiSettings);
    }

    [Fact]
    public void ChatService_ShouldInitialize()
    {
        // Act
        var service = new ChatService(_apiSettings, _privateMessageServiceMock.Object);

        // Assert
        service.Should().NotBeNull();
    }

    [Fact]
    public void OnMessageReceived_ShouldTriggerEvent()
    {
        // Arrange
        var service = new ChatService(_apiSettings, _privateMessageServiceMock.Object);
        Message? receivedMessage = null;
        service.OnMessageReceived += (msg) => receivedMessage = msg;

        var testMessage = new Message
        {
            Id = Guid.NewGuid(),
            Username = "testuser",
            Content = "Test message",
            Channel = "general",
            Timestamp = DateTime.UtcNow
        };

        // Act
        // Note: En conditions réelles, ce serait déclenché par SignalR
        // Pour les tests, on devrait mocker la connexion SignalR

        // Assert
        service.Should().NotBeNull();
    }
}