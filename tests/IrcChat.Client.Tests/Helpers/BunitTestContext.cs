// tests/IrcChat.Client.Tests/Helpers/BunitBunitContext.cs
using Bunit;
using IrcChat.Client.Models;
using IrcChat.Client.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;

namespace IrcChat.Client.Tests.Helpers;

public class BunitBunitContext : BunitContext
{
    public Mock<LocalStorageService> LocalStorageMock { get; }
    public Mock<HttpClient> HttpClientMock { get; }
    public Mock<UnifiedAuthService> AuthServiceMock { get; }

    public BunitBunitContext()
    {
        LocalStorageMock = new Mock<LocalStorageService>(MockBehavior.Strict, JSInterop.JSRuntime);
        HttpClientMock = new Mock<HttpClient>(MockBehavior.Strict);
        AuthServiceMock = new Mock<UnifiedAuthService>(
            MockBehavior.Strict,
            LocalStorageMock.Object,
            HttpClientMock.Object);

        // Configuration par défaut du LocalStorage
        LocalStorageMock
            .Setup(x => x.GetItemAsync(It.IsAny<string>()))
            .ReturnsAsync((string?)null);

        LocalStorageMock
            .Setup(x => x.SetItemAsync(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        // Enregistrer les services
        Services.AddSingleton(LocalStorageMock.Object);
        Services.AddSingleton(HttpClientMock.Object);
        Services.AddSingleton(AuthServiceMock.Object);

        // Configuration de l'API
        Services.AddSingleton(Options.Create(new ApiSettings
        {
            BaseUrl = "https://localhost:7000",
            SignalRHubUrl = "https://localhost:7000/chathub"
        }));
    }
}