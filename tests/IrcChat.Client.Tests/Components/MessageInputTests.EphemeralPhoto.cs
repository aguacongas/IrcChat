using IrcChat.Client.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace IrcChat.Client.Tests.Components;

public partial class MessageInputTests
{
    // ==================== Tests Photos Éphémères ====================

    [Fact]
    public async Task FileInput_ValidFile_UploadsAndSendsPhoto()
    {
        // Arrange
        var userId = "user123";
        var imageUrl = "https://cloudinary.com/image.jpg";
        var thumbnailUrl = "https://cloudinary.com/thumb.jpg";
        var photoSent = false;
        (string ImageUrl, string ThumbnailUrl) capturedPhoto = default;

        _authServiceMock
            .Setup(x => x.GetClientUserIdAsync())
            .ReturnsAsync(userId);

        _ephemeralPhotoServiceMock
            .Setup(x => x.ValidateImageFileAsync(It.IsAny<IBrowserFile>()))
            .ReturnsAsync(true);

        _ephemeralPhotoServiceMock
            .Setup(x => x.UploadImageAsync(It.IsAny<string>(), userId))
            .ReturnsAsync((imageUrl, thumbnailUrl));

        var cut = Render<MessageInput>(parameters => parameters
            .Add(p => p.IsConnected, true)
            .Add(p => p.AvailableUsers, [])
            .Add(p => p.OnSendPhoto, photo =>
            {
                photoSent = true;
                capturedPhoto = photo;
            }));

        var mockFile = CreateMockBrowserFile("test.jpg", "image/jpeg", 1024 * 1024);
        var inputFile = cut.FindComponent<InputFile>();

        // Act
        await cut.InvokeAsync(() => inputFile.Instance.OnChange.InvokeAsync(new InputFileChangeEventArgs([mockFile.Object])));
        await Task.Delay(150); // Attendre la fin du traitement async

        // Assert
        Assert.True(photoSent);
        Assert.Equal(imageUrl, capturedPhoto.ImageUrl);
        Assert.Equal(thumbnailUrl, capturedPhoto.ThumbnailUrl);

        _ephemeralPhotoServiceMock.Verify(
            x => x.ValidateImageFileAsync(It.IsAny<IBrowserFile>()),
            Times.Once);

        _ephemeralPhotoServiceMock.Verify(
            x => x.UploadImageAsync(It.IsAny<string>(), userId),
            Times.Once);
    }

    [Fact]
    public async Task FileInput_InvalidFile_ShowsError()
    {
        // Arrange
        _ephemeralPhotoServiceMock
            .Setup(x => x.ValidateImageFileAsync(It.IsAny<IBrowserFile>()))
            .ReturnsAsync(false);

        var cut = Render<MessageInput>(parameters => parameters
            .Add(p => p.IsConnected, true)
            .Add(p => p.AvailableUsers, []));

        var mockFile = CreateMockBrowserFile("test.pdf", "application/pdf", 1024);
        var inputFile = cut.FindComponent<InputFile>();

        // Act
        await cut.InvokeAsync(() => inputFile.Instance.OnChange.InvokeAsync(new InputFileChangeEventArgs([mockFile.Object])));
        await Task.Delay(150);

        // Assert
        var errorToast = cut.Find(".upload-error-toast");
        Assert.NotNull(errorToast);
        Assert.Contains("Fichier invalide", errorToast.TextContent);
    }

    [Fact]
    public async Task FileInput_UploadError_ShowsErrorToast()
    {
        // Arrange
        var userId = "user123";

        _authServiceMock
            .Setup(x => x.GetClientUserIdAsync())
            .ReturnsAsync(userId);

        _ephemeralPhotoServiceMock
            .Setup(x => x.ValidateImageFileAsync(It.IsAny<IBrowserFile>()))
            .ReturnsAsync(true);

        _ephemeralPhotoServiceMock
            .Setup(x => x.UploadImageAsync(It.IsAny<string>(), userId))
            .ThrowsAsync(new HttpRequestException("Upload failed"));

        var cut = Render<MessageInput>(parameters => parameters
            .Add(p => p.IsConnected, true)
            .Add(p => p.AvailableUsers, []));

        var mockFile = CreateMockBrowserFile("test.jpg", "image/jpeg", 1024 * 1024);
        var inputFile = cut.FindComponent<InputFile>();

        // Act
        await cut.InvokeAsync(() => inputFile.Instance.OnChange.InvokeAsync(new InputFileChangeEventArgs([mockFile.Object])));
        await Task.Delay(150);

        // Assert
        var errorToast = cut.Find(".upload-error-toast");
        Assert.NotNull(errorToast);
        Assert.Contains("Erreur lors de l'envoi de la photo", errorToast.TextContent);
    }

    [Fact]
    public async Task FileInput_WhileUploading_ShowsSpinner()
    {
        // Arrange
        var userId = "user123";
        var uploadStarted = new TaskCompletionSource<bool>();

        _authServiceMock
            .Setup(x => x.GetClientUserIdAsync())
            .ReturnsAsync(userId);

        _ephemeralPhotoServiceMock
            .Setup(x => x.ValidateImageFileAsync(It.IsAny<IBrowserFile>()))
            .ReturnsAsync(true);

        _ephemeralPhotoServiceMock
            .Setup(x => x.UploadImageAsync(It.IsAny<string>(), userId))
            .Returns(async () =>
            {
                uploadStarted.SetResult(true);
                await Task.Delay(500); // Upload lent
                return ("url", "thumb");
            });

        var cut = Render<MessageInput>(parameters => parameters
            .Add(p => p.IsConnected, true)
            .Add(p => p.AvailableUsers, []));

        var mockFile = CreateMockBrowserFile("test.jpg", "image/jpeg", 1024 * 1024);
        var inputFile = cut.FindComponent<InputFile>();

        // Act
        var task = cut.InvokeAsync(() => inputFile.Instance.OnChange.InvokeAsync(new InputFileChangeEventArgs([mockFile.Object])));
        await uploadStarted.Task; // Attendre que l'upload commence

        // Assert - Pendant l'upload, le spinner devrait être visible
        var spinner = cut.Find(".spinner-small");
        Assert.NotNull(spinner);

        await task; // Attendre la fin de l'upload
        cut.Render(); // Re-render pour mettre à jour l'UI

        // Après l'upload, le spinner ne devrait plus être là
        Assert.Empty(cut.FindAll(".spinner-small"));
    }

    [Fact]
    public void CameraButton_Click_OpensModal()
    {
        // Arrange
        var cut = Render<MessageInput>(parameters => parameters
            .Add(p => p.IsConnected, true)
            .Add(p => p.AvailableUsers, []));

        // Act
        var cameraButton = cut.Find(".camera-button");
        cameraButton.Click();

        // Assert
        var cameraModal = cut.FindComponent<CameraModal>();
        Assert.NotNull(cameraModal);
        Assert.True(cameraModal.Instance.IsOpen);
    }

    [Fact]
    public async Task CameraModal_OnClose_ClosesModal()
    {
        // Arrange
        var cut = Render<MessageInput>(parameters => parameters
            .Add(p => p.IsConnected, true)
            .Add(p => p.AvailableUsers, []));

        var cameraButton = cut.Find(".camera-button");
        cameraButton.Click();

        var cameraModal = cut.FindComponent<CameraModal>();
        Assert.True(cameraModal.Instance.IsOpen);

        // Act - Déclencher OnClose via le bouton close de la modal
        var closeButton = cut.Find(".btn-close");
        await closeButton.ClickAsync();

        // Assert
        Assert.False(cameraModal.Instance.IsOpen);
    }

    [Fact]
    public async Task CameraModal_OnPhotoSent_UploadsAndSendsPhoto()
    {
        // Arrange
        var userId = "user123";
        var base64Photo = "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg==";
        var imageUrl = "https://cloudinary.com/image.jpg";
        var thumbnailUrl = "https://cloudinary.com/thumb.jpg";
        var photoSent = false;
        (string ImageUrl, string ThumbnailUrl) capturedPhoto = default;

        _authServiceMock
            .Setup(x => x.GetClientUserIdAsync())
            .ReturnsAsync(userId);

        _ephemeralPhotoServiceMock
            .Setup(x => x.UploadImageAsync(base64Photo, userId))
            .ReturnsAsync((imageUrl, thumbnailUrl));

        _ephemeralPhotoServiceMock
            .Setup(x => x.IsCameraAvailableAsync())
            .ReturnsAsync(true);

        _ephemeralPhotoServiceMock
            .Setup(x => x.CapturePhotoAsync(It.IsAny<string>()))
            .ReturnsAsync(base64Photo);

        var cut = Render<MessageInput>(parameters => parameters
            .Add(p => p.IsConnected, true)
            .Add(p => p.AvailableUsers, [])
            .Add(p => p.OnSendPhoto, photo =>
            {
                photoSent = true;
                capturedPhoto = photo;
            }));

        // Ouvrir la modal
        var cameraButton = cut.Find(".camera-button");
        cameraButton.Click();
        await Task.Delay(200); // Attendre initialisation caméra

        // Capturer la photo
        var captureButton = cut.Find(".btn-capture");
        await captureButton.ClickAsync();
        await Task.Delay(100);

        // Act - Envoyer la photo
        var sendButton = cut.Find(".btn-send");
        await sendButton.ClickAsync();
        await Task.Delay(150);

        // Assert
        Assert.True(photoSent);
        Assert.Equal(imageUrl, capturedPhoto.ImageUrl);
        Assert.Equal(thumbnailUrl, capturedPhoto.ThumbnailUrl);

        _ephemeralPhotoServiceMock.Verify(
            x => x.UploadImageAsync(base64Photo, userId),
            Times.Once);
    }

    [Fact]
    public async Task CameraModal_UploadError_ShowsErrorToast()
    {
        // Arrange
        var userId = "user123";
        var base64Photo = "data:image/png;base64,iVBORw0KGgo=";

        _authServiceMock
            .Setup(x => x.GetClientUserIdAsync())
            .ReturnsAsync(userId);

        _ephemeralPhotoServiceMock
            .Setup(x => x.UploadImageAsync(base64Photo, userId))
            .ThrowsAsync(new HttpRequestException("Upload failed"));

        _ephemeralPhotoServiceMock
            .Setup(x => x.IsCameraAvailableAsync())
            .ReturnsAsync(true);

        _ephemeralPhotoServiceMock
            .Setup(x => x.CapturePhotoAsync(It.IsAny<string>()))
            .ReturnsAsync(base64Photo);

        var cut = Render<MessageInput>(parameters => parameters
            .Add(p => p.IsConnected, true)
            .Add(p => p.AvailableUsers, []));

        // Ouvrir la modal et capturer
        var cameraButton = cut.Find(".camera-button");
        cameraButton.Click();
        await Task.Delay(200);

        var captureButton = cut.Find(".btn-capture");
        await captureButton.ClickAsync();
        await Task.Delay(100);

        // Act - Tenter d'envoyer
        var sendButton = cut.Find(".btn-send");
        await sendButton.ClickAsync();
        await Task.Delay(150);

        // Assert
        var errorToast = cut.Find(".upload-error-toast");
        Assert.NotNull(errorToast);
        Assert.Contains("Erreur lors de l'envoi de la photo", errorToast.TextContent);
    }

    [Fact]
    public async Task CameraModal_AfterPhotoSent_ClosesModal()
    {
        // Arrange
        var userId = "user123";
        var base64Photo = "data:image/png;base64,iVBORw0KGgo=";

        _authServiceMock
            .Setup(x => x.GetClientUserIdAsync())
            .ReturnsAsync(userId);

        _ephemeralPhotoServiceMock
            .Setup(x => x.UploadImageAsync(base64Photo, userId))
            .ReturnsAsync(("url", "thumb"));

        _ephemeralPhotoServiceMock
            .Setup(x => x.IsCameraAvailableAsync())
            .ReturnsAsync(true);

        _ephemeralPhotoServiceMock
            .Setup(x => x.CapturePhotoAsync(It.IsAny<string>()))
            .ReturnsAsync(base64Photo);

        var cut = Render<MessageInput>(parameters => parameters
            .Add(p => p.IsConnected, true)
            .Add(p => p.AvailableUsers, []));

        // Ouvrir, capturer
        var cameraButton = cut.Find(".camera-button");
        cameraButton.Click();
        await Task.Delay(200);

        var captureButton = cut.Find(".btn-capture");
        await captureButton.ClickAsync();
        await Task.Delay(100);

        var cameraModal = cut.FindComponent<CameraModal>();
        Assert.True(cameraModal.Instance.IsOpen);

        // Act - Envoyer
        var sendButton = cut.Find(".btn-send");
        await sendButton.ClickAsync();
        await Task.Delay(150);

        // Assert - Modal fermée
        Assert.False(cameraModal.Instance.IsOpen);
    }

    [Fact]
    public async Task ErrorToast_DisplaysFor4Seconds_ThenDisappears()
    {
        // Arrange
        _ephemeralPhotoServiceMock
            .Setup(x => x.ValidateImageFileAsync(It.IsAny<IBrowserFile>()))
            .ReturnsAsync(false);

        var cut = Render<MessageInput>(parameters => parameters
            .Add(p => p.IsConnected, true)
            .Add(p => p.AvailableUsers, []));

        var mockFile = CreateMockBrowserFile("test.pdf", "application/pdf", 1024);
        var inputFile = cut.FindComponent<InputFile>();

        // Act
        await cut.InvokeAsync(() => inputFile.Instance.OnChange.InvokeAsync(new InputFileChangeEventArgs([mockFile.Object])));
        await Task.Delay(150);

        // Assert - Toast visible
        var errorToast = cut.Find(".upload-error-toast");
        Assert.NotNull(errorToast);
        Assert.Contains("Fichier invalide", errorToast.TextContent);

        // Attendre 4+ secondes
        await Task.Delay(4100);

        // Assert - Toast disparu
        Assert.Empty(cut.FindAll(".upload-error-toast"));
    }

    [Fact]
    public void PhotoButton_DisabledWhenNotConnected()
    {
        // Arrange & Act
        var cut = Render<MessageInput>(parameters => parameters
            .Add(p => p.IsConnected, false)
            .Add(p => p.AvailableUsers, []));

        // Assert
        var photoButton = cut.Find(".photo-button");
        Assert.True(photoButton.HasAttribute("disabled"));
    }

    [Fact]
    public async Task PhotoButton_DisabledDuringUpload()
    {
        // Arrange
        var userId = "user123";
        var uploadStarted = new TaskCompletionSource<bool>();

        _authServiceMock
            .Setup(x => x.GetClientUserIdAsync())
            .ReturnsAsync(userId);

        _ephemeralPhotoServiceMock
            .Setup(x => x.ValidateImageFileAsync(It.IsAny<IBrowserFile>()))
            .ReturnsAsync(true);

        _ephemeralPhotoServiceMock
            .Setup(x => x.UploadImageAsync(It.IsAny<string>(), userId))
            .Returns(async () =>
            {
                uploadStarted.SetResult(true);
                await Task.Delay(500);
                return ("url", "thumb");
            });

        var cut = Render<MessageInput>(parameters => parameters
            .Add(p => p.IsConnected, true)
            .Add(p => p.AvailableUsers, []));

        var mockFile = CreateMockBrowserFile("test.jpg", "image/jpeg", 1024);
        var inputFile = cut.FindComponent<InputFile>();

        // Act
        var changeTask = cut.InvokeAsync(() => inputFile.Instance.OnChange.InvokeAsync(new InputFileChangeEventArgs([mockFile.Object])));
        await uploadStarted.Task;

        // Assert - Pendant l'upload
        var photoButton = cut.Find(".photo-button");
        Assert.True(photoButton.HasAttribute("disabled"));

        await changeTask;
    }

    [Fact]
    public void CameraButton_DisabledWhenNotConnected()
    {
        // Arrange & Act
        var cut = Render<MessageInput>(parameters => parameters
            .Add(p => p.IsConnected, false)
            .Add(p => p.AvailableUsers, []));

        // Assert
        var cameraButton = cut.Find(".camera-button");
        Assert.True(cameraButton.HasAttribute("disabled"));
    }

    // ==================== Helper Methods ====================

    /// <summary>
    /// Crée un mock de IBrowserFile pour les tests
    /// </summary>
    private static Mock<IBrowserFile> CreateMockBrowserFile(string name, string contentType, long size)
    {
        var mockFile = new Mock<IBrowserFile>();
        mockFile.Setup(f => f.Name).Returns(name);
        mockFile.Setup(f => f.ContentType).Returns(contentType);
        mockFile.Setup(f => f.Size).Returns(size);

        // Mock du stream
        var memoryStream = new MemoryStream(new byte[size]);
        mockFile.Setup(f => f.OpenReadStream(It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .Returns(memoryStream);

        return mockFile;
    }
}