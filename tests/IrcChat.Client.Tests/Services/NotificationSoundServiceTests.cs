// tests/IrcChat.Client.Tests/Services/NotificationSoundServiceTests.cs
using IrcChat.Client.Services;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace IrcChat.Client.Tests.Services;

public class NotificationSoundServiceTests : IAsyncDisposable
{
    private readonly Mock<IJSRuntime> _jsRuntimeMock;
    private readonly Mock<IJSObjectReference> _moduleMock;
    private readonly Mock<ILogger<NotificationSoundService>> _loggerMock;
    private readonly NotificationSoundService _service;

    public NotificationSoundServiceTests()
    {
        _jsRuntimeMock = new Mock<IJSRuntime>();
        _moduleMock = new Mock<IJSObjectReference>();
        _loggerMock = new Mock<ILogger<NotificationSoundService>>();

        // Setup par défaut : import du module réussit
        _jsRuntimeMock
            .Setup(x => x.InvokeAsync<IJSObjectReference>("import", It.Is<object[]>(args => args.Length == 1 && (string)args[0] == "./js/audioPlayer.js")))
            .ReturnsAsync(_moduleMock.Object);

        _service = new NotificationSoundService(_jsRuntimeMock.Object, _loggerMock.Object);
    }

    [Fact]
    public async Task PlaySoundAsync_WhenEnabled_ShouldImportModuleAndCallPlaySound()
    {
        // Arrange
        _jsRuntimeMock
            .Setup(x => x.InvokeAsync<string?>("localStorage.getItem", It.IsAny<object[]>()))
            .ReturnsAsync("true");

        _moduleMock
            .Setup(x => x.InvokeAsync<IJSVoidResult>("playSound", It.IsAny<object[]>()))
            .ReturnsAsync((IJSVoidResult)null!);

        // Act
        await _service.PlaySoundAsync();

        // Assert
        _jsRuntimeMock.Verify(
            x => x.InvokeAsync<IJSObjectReference>("import", It.Is<object[]>(args => args.Length == 1 && (string)args[0] == "./js/audioPlayer.js")),
            Times.Once);

        _moduleMock.Verify(
            x => x.InvokeAsync<IJSVoidResult>("playSound", It.IsAny<object[]>()),
            Times.Once);
    }

    [Fact]
    public async Task PlaySoundAsync_WhenCalledMultipleTimes_ShouldImportModuleOnlyOnce()
    {
        // Arrange
        _jsRuntimeMock
            .Setup(x => x.InvokeAsync<string?>("localStorage.getItem", It.IsAny<object[]>()))
            .ReturnsAsync("true");

        _moduleMock
            .Setup(x => x.InvokeAsync<IJSVoidResult>("playSound", It.IsAny<object[]>()))
            .ReturnsAsync((IJSVoidResult)null!);

        // Act
        await _service.PlaySoundAsync();
        await _service.PlaySoundAsync();
        await _service.PlaySoundAsync();

        // Assert - Module importé une seule fois (lazy loading)
        _jsRuntimeMock.Verify(
            x => x.InvokeAsync<IJSObjectReference>("import", It.IsAny<object[]>()),
            Times.Once);

        // playSound appelé 3 fois
        _moduleMock.Verify(
            x => x.InvokeAsync<IJSVoidResult>("playSound", It.IsAny<object[]>()),
            Times.Exactly(3));
    }

    [Fact]
    public async Task PlaySoundAsync_WhenDisabled_ShouldNotImportModuleOrCallPlaySound()
    {
        // Arrange
        _jsRuntimeMock
            .Setup(x => x.InvokeAsync<string?>("localStorage.getItem", It.IsAny<object[]>()))
            .ReturnsAsync("false");

        // Act
        await _service.PlaySoundAsync();

        // Assert
        _jsRuntimeMock.Verify(
            x => x.InvokeAsync<IJSObjectReference>("import", It.IsAny<object[]>()),
            Times.Never);

        _moduleMock.Verify(
            x => x.InvokeAsync<IJSVoidResult>("playSound", It.IsAny<object[]>()),
            Times.Never);
    }

    [Fact]
    public async Task PlaySoundAsync_WhenModuleImportFails_ShouldNotThrowException()
    {
        // Arrange
        _jsRuntimeMock
            .Setup(x => x.InvokeAsync<string?>("localStorage.getItem", It.IsAny<object[]>()))
            .ReturnsAsync("true");

        _jsRuntimeMock
            .Setup(x => x.InvokeAsync<IJSObjectReference>("import", It.IsAny<object[]>()))
            .ThrowsAsync(new JSException("Module not found"));

        // Act & Assert - Ne doit pas throw
        await _service.PlaySoundAsync();

        // Vérifier que l'erreur a été loggée
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Impossible de charger le module audioPlayer")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task PlaySoundAsync_AfterModuleImportFails_ShouldNotRetryImport()
    {
        // Arrange
        _jsRuntimeMock
            .Setup(x => x.InvokeAsync<string?>("localStorage.getItem", It.IsAny<object[]>()))
            .ReturnsAsync("true");

        _jsRuntimeMock
            .Setup(x => x.InvokeAsync<IJSObjectReference>("import", It.IsAny<object[]>()))
            .ThrowsAsync(new JSException("Module not found"));

        // Act - Premier appel échoue
        await _service.PlaySoundAsync();

        // Act - Deuxième appel ne doit pas réessayer l'import
        await _service.PlaySoundAsync();
        await _service.PlaySoundAsync();

        // Assert - Import tenté une seule fois
        _jsRuntimeMock.Verify(
            x => x.InvokeAsync<IJSObjectReference>("import", It.IsAny<object[]>()),
            Times.Once);
    }

    [Fact]
    public async Task PlaySoundAsync_WhenPlaySoundThrows_ShouldNotThrowException()
    {
        // Arrange
        _jsRuntimeMock
            .Setup(x => x.InvokeAsync<string?>("localStorage.getItem", It.IsAny<object[]>()))
            .ReturnsAsync("true");

        _moduleMock
            .Setup(x => x.InvokeAsync<IJSVoidResult>("playSound", It.IsAny<object[]>()))
            .ThrowsAsync(new JSException("Audio error"));

        // Act & Assert - Ne doit pas throw
        await _service.PlaySoundAsync();

        // Vérifier que l'erreur a été loggée
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Erreur lors de la lecture du son")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task ToggleSoundAsync_WhenEnabled_ShouldDisable()
    {
        // Arrange
        _jsRuntimeMock
            .Setup(x => x.InvokeAsync<string?>("localStorage.getItem", It.IsAny<object[]>()))
            .ReturnsAsync("true");

        _jsRuntimeMock
            .Setup(x => x.InvokeAsync<IJSVoidResult>("localStorage.setItem", It.IsAny<object[]>()))
            .ReturnsAsync((IJSVoidResult)null!);

        // Act
        await _service.ToggleSoundAsync();

        // Assert
        _jsRuntimeMock.Verify(
            x => x.InvokeAsync<IJSVoidResult>(
                "localStorage.setItem",
                It.Is<object[]>(args =>
                    args.Length == 2 &&
                    (string)args[0] == "notification-sound-enabled" &&
                    (string)args[1] == "false")),
            Times.Once);
    }

    [Fact]
    public async Task ToggleSoundAsync_WhenDisabled_ShouldEnable()
    {
        // Arrange
        _jsRuntimeMock
            .Setup(x => x.InvokeAsync<string?>("localStorage.getItem", It.IsAny<object[]>()))
            .ReturnsAsync("false");

        _jsRuntimeMock
            .Setup(x => x.InvokeAsync<IJSVoidResult>("localStorage.setItem", It.IsAny<object[]>()))
            .ReturnsAsync((IJSVoidResult)null!);

        // Act
        await _service.ToggleSoundAsync();

        // Assert
        _jsRuntimeMock.Verify(
            x => x.InvokeAsync<IJSVoidResult>(
                "localStorage.setItem",
                It.Is<object[]>(args =>
                    args.Length == 2 &&
                    (string)args[0] == "notification-sound-enabled" &&
                    (string)args[1] == "true")),
            Times.Once);
    }

    [Fact]
    public async Task IsSoundEnabledAsync_WhenNoPreference_ShouldReturnTrue()
    {
        // Arrange
        _jsRuntimeMock
            .Setup(x => x.InvokeAsync<string?>("localStorage.getItem", It.IsAny<object[]>()))
            .ReturnsAsync((string?)null);

        // Act
        var result = await _service.IsSoundEnabledAsync();

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task IsSoundEnabledAsync_WhenEnabled_ShouldReturnTrue()
    {
        // Arrange
        _jsRuntimeMock
            .Setup(x => x.InvokeAsync<string?>("localStorage.getItem", It.IsAny<object[]>()))
            .ReturnsAsync("true");

        // Act
        var result = await _service.IsSoundEnabledAsync();

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task IsSoundEnabledAsync_WhenDisabled_ShouldReturnFalse()
    {
        // Arrange
        _jsRuntimeMock
            .Setup(x => x.InvokeAsync<string?>("localStorage.getItem", It.IsAny<object[]>()))
            .ReturnsAsync("false");

        // Act
        var result = await _service.IsSoundEnabledAsync();

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task IsSoundEnabledAsync_WhenInvalidValue_ShouldReturnFalse()
    {
        // Arrange
        _jsRuntimeMock
            .Setup(x => x.InvokeAsync<string?>("localStorage.getItem", It.IsAny<object[]>()))
            .ReturnsAsync("invalid");

        // Act
        var result = await _service.IsSoundEnabledAsync();

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task IsSoundEnabledAsync_WhenJSThrows_ShouldReturnTrue()
    {
        // Arrange
        _jsRuntimeMock
            .Setup(x => x.InvokeAsync<string?>("localStorage.getItem", It.IsAny<object[]>()))
            .ThrowsAsync(new JSException("LocalStorage error"));

        // Act
        var result = await _service.IsSoundEnabledAsync();

        // Assert
        Assert.True(result); // Défaut : activé

        // Vérifier que l'erreur a été loggée
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Erreur lors de la lecture de la préférence")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task ToggleSoundAsync_WhenJSThrows_ShouldThrowException()
    {
        // Arrange
        _jsRuntimeMock
            .Setup(x => x.InvokeAsync<string?>("localStorage.getItem", It.IsAny<object[]>()))
            .ReturnsAsync("true");

        _jsRuntimeMock
            .Setup(x => x.InvokeAsync<IJSVoidResult>("localStorage.setItem", It.IsAny<object[]>()))
            .ThrowsAsync(new JSException("LocalStorage error"));

        // Act & Assert
        await Assert.ThrowsAsync<JSException>(() => _service.ToggleSoundAsync());

        // Vérifier que l'erreur a été loggée
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Erreur lors du changement d'état")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task DisposeAsync_WhenModuleLoaded_ShouldDisposeModule()
    {
        // Arrange
        _jsRuntimeMock
            .Setup(x => x.InvokeAsync<string?>("localStorage.getItem", It.IsAny<object[]>()))
            .ReturnsAsync("true");

        _moduleMock
            .Setup(x => x.InvokeAsync<IJSVoidResult>("playSound", It.IsAny<object[]>()))
            .ReturnsAsync((IJSVoidResult)null!);

        _moduleMock
            .Setup(x => x.DisposeAsync())
            .Returns(ValueTask.CompletedTask);

        // Act - Charger le module
        await _service.PlaySoundAsync();

        // Act - Dispose
        await _service.DisposeAsync();

        // Assert
        _moduleMock.Verify(x => x.DisposeAsync(), Times.Once);
    }

    [Fact]
    public async Task DisposeAsync_WhenModuleNotLoaded_ShouldNotThrow()
    {
        // Act & Assert - Ne doit pas throw
        await _service.DisposeAsync();

        // Module jamais chargé, donc pas de dispose
        _moduleMock.Verify(x => x.DisposeAsync(), Times.Never);
    }

    [Fact]
    public async Task DisposeAsync_WhenModuleDisposeThrows_ShouldNotThrow()
    {
        // Arrange
        _jsRuntimeMock
            .Setup(x => x.InvokeAsync<string?>("localStorage.getItem", It.IsAny<object[]>()))
            .ReturnsAsync("true");

        _moduleMock
            .Setup(x => x.InvokeAsync<IJSVoidResult>("playSound", It.IsAny<object[]>()))
            .ReturnsAsync((IJSVoidResult)null!);

        _moduleMock
            .Setup(x => x.DisposeAsync())
            .ThrowsAsync(new JSException("Dispose error"));

        // Act - Charger le module
        await _service.PlaySoundAsync();

        // Act & Assert - Ne doit pas throw
        await _service.DisposeAsync();

        // Vérifier que l'erreur a été loggée
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Erreur lors du dispose du module audioPlayer")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    public async ValueTask DisposeAsync()
    {
        await _service.DisposeAsync();
        GC.SuppressFinalize(this);
    }
}