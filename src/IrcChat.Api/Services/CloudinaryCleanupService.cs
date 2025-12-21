using CloudinaryDotNet.Actions;
using Microsoft.Extensions.Options;

namespace IrcChat.Api.Services;

/// <summary>
/// Service de nettoyage automatique des images Cloudinary de plus de 24h.
/// </summary>
public class CloudinaryCleanupService(
    ICloudinaryWrapper cloudinaryWrapper,
    IOptions<CloudinaryOptions> options,
    ILogger<CloudinaryCleanupService> logger) : BackgroundService
{
    private readonly TimeSpan _cleanupInterval = TimeSpan.FromHours(6); // Nettoyer toutes les 6h

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("CloudinaryCleanupService démarré");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CleanupOldImagesAsync(stoppingToken);

                await Task.Delay(_cleanupInterval, stoppingToken);

                if (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
            }
            catch (TaskCanceledException)
            {
                // Arrêt normal
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Erreur lors du nettoyage Cloudinary");
            }
        }

        logger.LogInformation("CloudinaryCleanupService arrêté");
    }

    private async Task CleanupOldImagesAsync(CancellationToken stoppingToken)
    {
        try
        {
            var settings = options.Value;
            var folder = settings.EphemeralFolder;
            var cutoffDate = DateTime.UtcNow.AddHours(-1);

            logger.LogInformation("Nettoyage des images Cloudinary de plus de 1h dans {Folder}", folder);

            // Lister les ressources du dossier
            var listParams = new ListResourcesByAssetFolderParams
            {
                AssetFolder = folder,
                Type = "upload",
                MaxResults = 500,
                StartAt = cutoffDate
            };

            var listResult = await cloudinaryWrapper.ListResourcesAsync(listParams, stoppingToken);

            if (listResult.Resources == null || listResult.Resources.Length == 0)
            {
                logger.LogInformation("Aucune image de plus de 1h trouvée");
                return;
            }

            var imagesToDelete = listResult.Resources
                .Select(r => r.PublicId)
                .ToList();

            logger.LogInformation("Suppression de {Count} images", imagesToDelete.Count);

            // Supprimer par batch
            foreach (var batch in imagesToDelete.Chunk(100))
            {
                var deleteParams = new DelResParams
                {
                    PublicIds = [.. batch],
                    Type = "upload"
                };

                var deleteResult = await cloudinaryWrapper.DeleteResourcesAsync(deleteParams, stoppingToken);
                if (deleteResult.Error != null)
                {
                    logger.LogError("{Message}", deleteResult.Error.Message);
                }
                else
                {
                    logger.LogInformation(
                        "Batch supprimé: {Deleted} images",
                        deleteResult.Deleted.Count);
                }
            }

            logger.LogInformation("Nettoyage terminé: {Count} images supprimées", imagesToDelete.Count);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Erreur lors du nettoyage des images");
        }
    }
}