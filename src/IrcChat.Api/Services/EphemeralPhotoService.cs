using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;
using Image = SixLabors.ImageSharp.Image;

namespace IrcChat.Api.Services;

/// <summary>
/// Implémentation du service de gestion des photos éphémères.
/// </summary>
[SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "Constantes")]
public class EphemeralPhotoService(ILogger<EphemeralPhotoService> logger) : IEphemeralPhotoService
{
    private static readonly int MaxPhotosPerMinute = 5;
    private static readonly int RateLimitWindowMinutes = 1;
    private static readonly int MaxImageWidth = 1920;
    private static readonly int MaxImageHeight = 1080;
    private static readonly int ThumbnailSize = 200;

    // Rate limiting : userId -> Queue de timestamps
    private static readonly ConcurrentDictionary<string, Queue<DateTime>> _rateLimitStore = new();

    public async Task<bool> ValidateImageAsync(byte[] imageBytes, int maxSizeKb)
    {
        if (imageBytes is null)
        {
            logger.LogWarning("Image null");
            return false;
        }

        try
        {
            // Vérifier la taille
            var sizeKb = imageBytes.Length / 1024;
            if (sizeKb > maxSizeKb)
            {
                logger.LogWarning("Image trop volumineuse: {SizeKb}KB (max: {MaxSizeKb}KB)", sizeKb, maxSizeKb);
                return false;
            }

            // Vérifier le format avec ImageSharp
            using var ms = new MemoryStream(imageBytes);
            var image = await Image.LoadAsync(ms);

            if (image.Width == 0 || image.Height == 0)
            {
                logger.LogWarning("Image invalide: dimensions nulles");
                return false;
            }

            logger.LogInformation("Image valide: {Width}x{Height}, {SizeKb}KB", image.Width, image.Height, sizeKb);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Erreur lors de la validation de l'image");
            return false;
        }
    }

    public async Task<byte[]> GenerateBlurredThumbnailAsync(byte[] imageBytes)
    {
        using var ms = new MemoryStream(imageBytes);
        using var image = await Image.LoadAsync(ms);

        // Redimensionner en thumbnail
        image.Mutate(x => x.Resize(new ResizeOptions
        {
            Size = new Size(ThumbnailSize, ThumbnailSize),
            Mode = ResizeMode.Max
        }));

        // Flouter
        image.Mutate(x => x.GaussianBlur(20));

        // Convertir en Base64
        using var outputMs = new MemoryStream();
        await image.SaveAsJpegAsync(outputMs, new JpegEncoder { Quality = 75 });
        var thumbnailBytes = outputMs.ToArray();

        logger.LogInformation("Thumbnail floutée générée: {Size} bytes", thumbnailBytes.Length);
        return thumbnailBytes;
    }

    public async Task<byte[]> CompressImageAsync(byte[] imageBytes, int quality)
    {
        using var ms = new MemoryStream(imageBytes);
        using var image = await Image.LoadAsync(ms);

        // Redimensionner si trop grande
        if (image.Width > MaxImageWidth || image.Height > MaxImageHeight)
        {
            image.Mutate(x => x.Resize(new ResizeOptions
            {
                Size = new Size(MaxImageWidth, MaxImageHeight),
                Mode = ResizeMode.Max
            }));
        }

        // Compresser en JPEG
        using var outputMs = new MemoryStream();
        await image.SaveAsJpegAsync(outputMs, new JpegEncoder { Quality = quality });
        var compressedBytes = outputMs.ToArray();

        logger.LogInformation("Image compressée: {OriginalSize}KB -> {CompressedSize}KB",
            imageBytes.Length / 1024, compressedBytes.Length / 1024);

        return compressedBytes;
    }

    public bool CheckRateLimit(string userId)
    {
        var now = DateTime.UtcNow;
        var windowStart = now.AddMinutes(-RateLimitWindowMinutes);

        var timestamps = _rateLimitStore.GetOrAdd(userId, _ => new Queue<DateTime>());

        lock (timestamps)
        {
            // Nettoyer les vieux timestamps
            while (timestamps.Count > 0 && timestamps.Peek() < windowStart)
            {
                timestamps.Dequeue();
            }

            // Vérifier le rate limit
            if (timestamps.Count >= MaxPhotosPerMinute)
            {
                logger.LogWarning("Rate limit dépassé pour l'utilisateur {UserId}: {Count}/{Max} photos",
                    userId, timestamps.Count, MaxPhotosPerMinute);
                return false;
            }

            return true;
        }
    }

    public void RecordPhotoSent(string userId)
    {
        var now = DateTime.UtcNow;
        var timestamps = _rateLimitStore.GetOrAdd(userId, _ => new Queue<DateTime>());

        lock (timestamps)
        {
            timestamps.Enqueue(now);
            logger.LogInformation("Photo envoyée enregistrée pour {UserId}: {Count}/{Max}",
                userId, timestamps.Count, MaxPhotosPerMinute);
        }
    }
}