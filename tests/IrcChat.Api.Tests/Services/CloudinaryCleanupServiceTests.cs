using CloudinaryDotNet.Actions;
using IrcChat.Api.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace IrcChat.Api.Tests.Services;

public class CloudinaryCleanupServiceTests
{
    private readonly ICloudinaryWrapper _cloudinary;
    private readonly Mock<ICloudinaryWrapper> _cloudinaryWrapperMock;
    private readonly Mock<ILogger<CloudinaryCleanupService>> _loggerMock;
    private readonly IOptions<CloudinaryOptions> _options;

    public CloudinaryCleanupServiceTests()
    {
        _cloudinaryWrapperMock = new Mock<ICloudinaryWrapper>();
        _cloudinary = _cloudinaryWrapperMock.Object;

        _loggerMock = new Mock<ILogger<CloudinaryCleanupService>>();

        var cloudinaryOptions = new CloudinaryOptions
        {
            CloudName = "test-cloud",
            ApiKey = "test-key",
            ApiSecret = "test-secret",
            EphemeralFolder = "test-folder",
            SignedUrlExpirationHours = 24
        };
        _options = Options.Create(cloudinaryOptions);
    }

    [Fact]
    public async Task CleanupOldImagesAsync_WithNoImages_ShouldLogAndReturn()
    {
        // Arrange
        var service = new CloudinaryCleanupService(
            _cloudinary,
            _options,
            _loggerMock.Object);

        var listResult = new ListResourcesResult
        {
            Resources = []
        };

        var cts = new CancellationTokenSource();

        _cloudinaryWrapperMock.Setup(x => x.ListResourcesAsync(It.IsAny<ListResourcesParams>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(listResult);

        // Act
        var task = service.StartAsync(cts.Token);
        await Task.Delay(100); // Laisser le service démarrer
        cts.Cancel();
        await task;

        // Assert
        _cloudinaryWrapperMock.Verify(
            x => x.ListResourcesAsync(It.IsAny<ListResourcesParams>(), It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);

        //// Vérifier qu'aucune suppression n'a été tentée
        _cloudinaryWrapperMock.Verify(
            x => x.DeleteResourcesAsync(It.IsAny<DelResParams>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task CleanupOldImagesAsync_WithOldImages_ShouldDeleteThem()
    {
        // Arrange
        var service = new CloudinaryCleanupService(
            _cloudinary,
            _options,
            _loggerMock.Object);

        var oldDate = DateTime.UtcNow.AddHours(-25).ToString("yyyy-MM-ddTHH:mm:ssZ");

        var listResult = new ListResourcesResult
        {
            Resources =
            [
                new Resource
                {
                    PublicId = "test-folder/user1/image1",
                    CreatedAt = oldDate
                },
                new Resource
                {
                    PublicId = "test-folder/user2/image2",
                    CreatedAt = oldDate
                }
            ]
        };

        var deleteResult = new DelResResult
        {
            Deleted = new Dictionary<string, string>
            {
                ["test-folder/user1/image1"] = "deleted",
                ["test-folder/user2/image2"] = "deleted"
            }
        };

        _cloudinaryWrapperMock
            .Setup(x => x.ListResourcesAsync(It.IsAny<ListResourcesParams>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(listResult);

        _cloudinaryWrapperMock
            .Setup(x => x.DeleteResourcesAsync(It.IsAny<DelResParams>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(deleteResult);

        var cts = new CancellationTokenSource();

        // Act
        var task = service.StartAsync(cts.Token);
        await Task.Delay(200); // Laisser le cleanup s'exécuter
        cts.Cancel();
        await task;

        // Assert
        _cloudinaryWrapperMock.Verify(
            x => x.DeleteResourcesAsync(
                It.Is<DelResParams>(p => p.PublicIds.Count == 2), It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task CleanupOldImagesAsync_WithCloudinaryError_ShouldLogAndContinue()
    {
        // Arrange
        var service = new CloudinaryCleanupService(
            _cloudinary,
            _options,
            _loggerMock.Object);

        _cloudinaryWrapperMock
            .Setup(x => x.ListResourcesAsync(It.IsAny<ListResourcesParams>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Cloudinary API error"));

        var cts = new CancellationTokenSource();

        // Act
        var task = service.StartAsync(cts.Token);
        await Task.Delay(200);
        cts.Cancel();

        // Le service ne doit pas crasher
        var exception = await Record.ExceptionAsync(async () => await task);

        // Assert
        Assert.Null(exception); // Le service doit gérer l'erreur gracieusement
    }

    [Fact]
    public async Task CleanupOldImagesAsync_WithLargeNumberOfImages_ShouldDeleteInBatches()
    {
        // Arrange
        var service = new CloudinaryCleanupService(
            _cloudinary,
            _options,
            _loggerMock.Object);

        var oldDate = DateTime.UtcNow.AddHours(-25).ToString("yyyy-MM-ddTHH:mm:ssZ");

        // Créer 150 images (plus que la taille de batch de 100)
        var resources = Enumerable.Range(1, 150)
            .Select(i => new Resource
            {
                PublicId = $"test-folder/user/image{i}",
                CreatedAt = oldDate
            })
            .ToArray();

        var listResult = new ListResourcesResult
        {
            Resources = resources
        };

        var deleteResult = new DelResResult
        {
            Deleted = []
        };

        _cloudinaryWrapperMock.Setup(x => x.ListResourcesAsync(It.IsAny<ListResourcesParams>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(listResult);

        _cloudinaryWrapperMock
            .Setup(x => x.DeleteResourcesAsync(It.IsAny<DelResParams>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(deleteResult);

        var cts = new CancellationTokenSource();

        // Act
        var task = service.StartAsync(cts.Token);
        await Task.Delay(300);
        cts.Cancel();
        await task;

        // Assert
        // Doit avoir appelé DeleteResourcesAsync au moins 2 fois (2 batches)
        _cloudinaryWrapperMock.Verify(
            x => x.DeleteResourcesAsync(It.IsAny<DelResParams>(), It.IsAny<CancellationToken>()),
            Times.AtLeast(2));
    }
}