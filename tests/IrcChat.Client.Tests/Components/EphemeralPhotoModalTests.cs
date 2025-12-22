using IrcChat.Client.Components;
using IrcChat.Client.Services;
using IrcChat.Shared.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace IrcChat.Client.Tests.Components;

/// <summary>
/// Tests pour le composant EphemeralPhotoModal.
/// </summary>
public class EphemeralPhotoModalTests : BunitContext
{
    private readonly Mock<IEphemeralPhotoService> _ephemeralPhotoServiceMock;
    private readonly Mock<ILogger<EphemeralPhotoModal>> _loggerMock;

    public EphemeralPhotoModalTests()
    {
        _ephemeralPhotoServiceMock = new Mock<IEphemeralPhotoService>();
        _loggerMock = new Mock<ILogger<EphemeralPhotoModal>>();

        // Configuration des mocks par défaut
        _ephemeralPhotoServiceMock
            .Setup(x => x.BlockDevToolsAsync())
            .Returns(Task.CompletedTask);

        _ephemeralPhotoServiceMock
            .Setup(x => x.DetectScreenshotAsync())
            .Returns(Task.CompletedTask);

        _ephemeralPhotoServiceMock
            .Setup(x => x.DestroyImageDataAsync(It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        // Enregistrement des services
        Services.AddSingleton(_ephemeralPhotoServiceMock.Object);
        Services.AddSingleton(_loggerMock.Object);
    }

    // ===== Rendering Tests =====

    [Fact]
    public void InitiallyHidden_WhenPhotoIsNull()
    {
        // Arrange & Act
        var cut = Render<EphemeralPhotoModal>(parameters => parameters
            .Add(p => p.Photo, null));

        // Assert
        Assert.DoesNotContain("ephemeral-modal-overlay", cut.Markup);
    }

    [Fact]
    public async Task Displayed_WhenPhotoProvided()
    {
        // Arrange
        var testPhoto = CreateTestPhoto();

        // Act
        var cut = Render<EphemeralPhotoModal>(parameters => parameters
            .Add(p => p.Photo, testPhoto));

        await Task.Delay(100); // Attendre l'initialisation

        // Assert
        Assert.Contains("ephemeral-modal-overlay", cut.Markup);
        Assert.Contains("ephemeral-modal-content", cut.Markup);
    }

    [Fact]
    public async Task ShowsImageWithCorrectUrl()
    {
        // Arrange
        var testPhoto = CreateTestPhoto();

        // Act
        var cut = Render<EphemeralPhotoModal>(parameters => parameters
            .Add(p => p.Photo, testPhoto));

        await Task.Delay(100);

        // Assert
        var img = cut.Find("img.ephemeral-photo");
        Assert.NotNull(img);
        Assert.Contains(testPhoto.ImageUrl!, img.GetAttribute("src"));
        Assert.Equal("Photo éphémère", img.GetAttribute("alt"));
    }

    [Fact]
    public async Task ShowsWatermark()
    {
        // Arrange
        var testPhoto = CreateTestPhoto();

        // Act
        var cut = Render<EphemeralPhotoModal>(parameters => parameters
            .Add(p => p.Photo, testPhoto));

        await Task.Delay(100);

        // Assert
        var watermark = cut.Find(".watermark");
        Assert.NotNull(watermark);
        Assert.Contains("Photo éphémère - Ne pas capturer", watermark.TextContent);
    }

    [Fact]
    public async Task ShowsDisclaimer()
    {
        // Arrange
        var testPhoto = CreateTestPhoto();

        // Act
        var cut = Render<EphemeralPhotoModal>(parameters => parameters
            .Add(p => p.Photo, testPhoto));

        await Task.Delay(100);

        // Assert
        var disclaimer = cut.Find(".disclaimer-text");
        Assert.NotNull(disclaimer);
        Assert.Contains("Photo éphémère - Protection non-absolue", disclaimer.TextContent);
    }

    // ===== Interaction Tests =====

    [Fact]
    public async Task OverlayClick_ClosesModal()
    {
        // Arrange
        var testPhoto = CreateTestPhoto();
        var onCloseCalled = false;

        var cut = Render<EphemeralPhotoModal>(parameters => parameters
            .Add(p => p.Photo, testPhoto)
            .Add(p => p.OnClose, () =>
            {
                onCloseCalled = true;
                return Task.CompletedTask;
            }));

        await Task.Delay(100);

        // Act
        var overlay = cut.Find(".ephemeral-modal-overlay");
        await cut.InvokeAsync(() => overlay.Click());
        await Task.Delay(100);

        // Assert
        Assert.True(onCloseCalled);
        Assert.DoesNotContain("ephemeral-modal-overlay", cut.Markup);
    }

    [Fact]
    public async Task CloseButtonClick_ClosesModal()
    {
        // Arrange
        var testPhoto = CreateTestPhoto();
        var onCloseCalled = false;

        var cut = Render<EphemeralPhotoModal>(parameters => parameters
            .Add(p => p.Photo, testPhoto)
            .Add(p => p.OnClose, () =>
            {
                onCloseCalled = true;
                return Task.CompletedTask;
            }));

        await Task.Delay(100);

        // Act
        var closeButton = cut.Find("button.modal-close");
        await cut.InvokeAsync(() => closeButton.Click());
        await Task.Delay(100);

        // Assert
        Assert.True(onCloseCalled);
        Assert.DoesNotContain("ephemeral-modal-overlay", cut.Markup);
    }

    // ===== Service Calls Tests =====

    [Fact]
    public async Task BlockDevTools_CalledOnOpen()
    {
        // Arrange
        var testPhoto = CreateTestPhoto();

        // Act
        var cut = Render<EphemeralPhotoModal>(parameters => parameters
            .Add(p => p.Photo, testPhoto));

        await Task.Delay(100);

        // Assert
        _ephemeralPhotoServiceMock.Verify(
            x => x.BlockDevToolsAsync(),
            Times.Once);
    }

    [Fact]
    public async Task DetectScreenshot_CalledOnOpen()
    {
        // Arrange
        var testPhoto = CreateTestPhoto();

        // Act
        var cut = Render<EphemeralPhotoModal>(parameters => parameters
            .Add(p => p.Photo, testPhoto));

        await Task.Delay(100);

        // Assert
        _ephemeralPhotoServiceMock.Verify(
            x => x.DetectScreenshotAsync(),
            Times.Once);
    }

    [Fact]
    public async Task DestroyImageData_CalledOnClose()
    {
        // Arrange
        var testPhoto = CreateTestPhoto();

        var cut = Render<EphemeralPhotoModal>(parameters => parameters
            .Add(p => p.Photo, testPhoto));

        await Task.Delay(100);

        // Act
        var closeButton = cut.Find("button.modal-close");
        await cut.InvokeAsync(() => closeButton.Click());
        await Task.Delay(100);

        // Assert
        _ephemeralPhotoServiceMock.Verify(
            x => x.DestroyImageDataAsync("ephemeral-photo-img"),
            Times.Once);
    }

    [Fact]
    public async Task ServiceMethods_NotCalled_WhenPhotoIsNull()
    {
        // Arrange & Act
        var cut = Render<EphemeralPhotoModal>(parameters => parameters
            .Add(p => p.Photo, null));

        await Task.Delay(100);

        // Assert
        _ephemeralPhotoServiceMock.Verify(
            x => x.BlockDevToolsAsync(),
            Times.Never);

        _ephemeralPhotoServiceMock.Verify(
            x => x.DetectScreenshotAsync(),
            Times.Never);
    }

    // ===== Timer Tests =====

    [Fact]
    public async Task AutoCloses_After3Seconds()
    {
        // Arrange
        var testPhoto = CreateTestPhoto();
        var onCloseCalled = false;

        var cut = Render<EphemeralPhotoModal>(parameters => parameters
            .Add(p => p.Photo, testPhoto)
            .Add(p => p.OnClose, () =>
            {
                onCloseCalled = true;
                return Task.CompletedTask;
            }));

        await Task.Delay(100); // Attendre l'initialisation

        // Assert - Modal visible initialement
        Assert.Contains("ephemeral-modal-overlay", cut.Markup);

        // Act - Attendre un peu plus que 3 secondes
        await Task.Delay(3200);

        // Assert - Modal fermée et callback appelé
        Assert.True(onCloseCalled);
        Assert.DoesNotContain("ephemeral-modal-overlay", cut.Markup);

        // Vérifier que DestroyImageData a été appelé
        _ephemeralPhotoServiceMock.Verify(
            x => x.DestroyImageDataAsync("ephemeral-photo-img"),
            Times.Once);
    }

    // ===== Lifecycle Tests =====

    [Fact]
    public async Task OnCloseCallback_InvokedOnClose()
    {
        // Arrange
        var testPhoto = CreateTestPhoto();
        var callbackInvoked = false;

        var cut = Render<EphemeralPhotoModal>(parameters => parameters
            .Add(p => p.Photo, testPhoto)
            .Add(p => p.OnClose, () =>
            {
                callbackInvoked = true;
                return Task.CompletedTask;
            }));

        await Task.Delay(100);

        // Act
        var closeButton = cut.Find("button.modal-close");
        await cut.InvokeAsync(() => closeButton.Click());
        await Task.Delay(100);

        // Assert
        Assert.True(callbackInvoked);
    }

    [Fact]
    public async Task PhotoImageUrl_SetToNullOnClose()
    {
        // Arrange
        var testPhoto = CreateTestPhoto();

        var cut = Render<EphemeralPhotoModal>(parameters => parameters
            .Add(p => p.Photo, testPhoto));

        await Task.Delay(100);

        // Vérifier que l'image est visible
        Assert.NotNull(testPhoto.ImageUrl);

        // Act
        var closeButton = cut.Find("button.modal-close");
        await cut.InvokeAsync(() => closeButton.Click());
        await Task.Delay(100);

        // Assert
        Assert.Null(testPhoto.ImageUrl);
    }

    [Fact]
    public async Task Dispose_DisposesTimer()
    {
        // Arrange
        var testPhoto = CreateTestPhoto();

        var cut = Render<EphemeralPhotoModal>(parameters => parameters
            .Add(p => p.Photo, testPhoto));

        await Task.Delay(100);

        // Act - Dispose du composant
        await cut.Instance.DisposeAsync();
        await Task.Delay(100);

        // Assert - Pas d'exception levée et le dispose est propre
        var exception = Record.Exception(() => cut.Dispose());
        Assert.Null(exception);
    }

    [Fact]
    public async Task MultipleCalls_ToOnParametersSetAsync_DoesNotReopenModal()
    {
        // Arrange
        var testPhoto = CreateTestPhoto();

        var cut = Render<EphemeralPhotoModal>(parameters => parameters
            .Add(p => p.Photo, testPhoto));

        await Task.Delay(100);

        // Fermer la modal
        var closeButton = cut.Find("button.modal-close");
        await cut.InvokeAsync(() => closeButton.Click());
        await Task.Delay(100);

        // Assert - Modal fermée
        Assert.DoesNotContain("ephemeral-modal-overlay", cut.Markup);

        // Act - Re-render avec la même photo
        cut.Render(parameters => parameters
            .Add(p => p.Photo, testPhoto));

        await Task.Delay(100);

        // Assert - Modal ne se rouvre pas (ImageUrl est null)
        Assert.DoesNotContain("ephemeral-modal-overlay", cut.Markup);
    }

    // ===== Error Handling Tests =====

    [Fact]
    public async Task ShowError_LoggedButDoesNotCrash()
    {
        // Arrange
        _ephemeralPhotoServiceMock
            .Setup(x => x.BlockDevToolsAsync())
            .ThrowsAsync(new InvalidOperationException("Test error"));

        var testPhoto = CreateTestPhoto();

        // Act
        var cut = Render<EphemeralPhotoModal>(parameters => parameters
            .Add(p => p.Photo, testPhoto));

        await Task.Delay(100);

        // Assert - Modal reste affichée malgré l'erreur
        Assert.Contains("ephemeral-modal-overlay", cut.Markup);

        // Vérifier que l'erreur a été loggée
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Erreur lors de l'ouverture de la modal")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task DestroyError_LoggedButDoesNotPreventClose()
    {
        // Arrange
        _ephemeralPhotoServiceMock
            .Setup(x => x.DestroyImageDataAsync(It.IsAny<string>()))
            .ThrowsAsync(new InvalidOperationException("Destroy error"));

        var testPhoto = CreateTestPhoto();
        var onCloseCalled = false;

        var cut = Render<EphemeralPhotoModal>(parameters => parameters
            .Add(p => p.Photo, testPhoto)
            .Add(p => p.OnClose, () =>
            {
                onCloseCalled = true;
                return Task.CompletedTask;
            }));

        await Task.Delay(100);

        // Act
        var closeButton = cut.Find("button.modal-close");
        await cut.InvokeAsync(() => closeButton.Click());
        await Task.Delay(100);

        // Assert - Modal fermée malgré l'erreur
        Assert.DoesNotContain("ephemeral-modal-overlay", cut.Markup);
        Assert.True(onCloseCalled);

        // Vérifier que l'erreur a été loggée
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Erreur lors de la destruction de l'image")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task DetectScreenshotError_LoggedButDoesNotPreventModalOpen()
    {
        // Arrange
        _ephemeralPhotoServiceMock
            .Setup(x => x.DetectScreenshotAsync())
            .ThrowsAsync(new InvalidOperationException("Screenshot detection error"));

        var testPhoto = CreateTestPhoto();

        // Act
        var cut = Render<EphemeralPhotoModal>(parameters => parameters
            .Add(p => p.Photo, testPhoto));

        await Task.Delay(100);
        cut.Render(); // Forcer le rendu après l'erreur

        // Assert - Modal affichée malgré l'erreur
        Assert.Contains("ephemeral-modal-overlay", cut.Markup);

        // Vérifier que l'erreur a été loggée
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Erreur lors de l'ouverture de la modal")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    // ===== Helper Methods =====

    private static EphemeralPhotoDto CreateTestPhoto()
    {
        return new EphemeralPhotoDto
        {
            Id = Guid.NewGuid(),
            SenderId = "user123",
            SenderUsername = "testuser",
            ImageUrl = "https://test.cloudinary.com/image.jpg",
            ThumbnailUrl = "https://test.cloudinary.com/thumbnail.jpg",
            ChannelId = "general",
            RecipientId = null,
            Timestamp = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddSeconds(3)
        };
    }
}