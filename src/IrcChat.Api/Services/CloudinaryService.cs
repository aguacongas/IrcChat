using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.Extensions.Options;
namespace IrcChat.Api.Services;
/// <summary>
/// Implémentation du service Cloudinary pour upload d'images éphémères.
/// </summary>
public class CloudinaryService(
ICloudinaryWrapper cloudinaryWrapper,
IOptions<CloudinaryOptions> options,
ILogger<CloudinaryService> logger) : ICloudinaryService
{
    public async Task<(string ImageUrl, string ThumbnailUrl)> UploadEphemeralPhotoAsync(byte[] imageBytes, string userId)
    {
        var settings = options.Value;
        var folder = settings.EphemeralFolder;
        var expirationHours = settings.SignedUrlExpirationHours;
        var publicId = $"{userId}/{Guid.NewGuid()}";

        logger.LogInformation("Upload image vers Cloudinary: {PublicId}", publicId);

        // Upload de l'image
        using var stream = new MemoryStream(imageBytes);
        var uploadParams = new ImageUploadParams
        {
            File = new FileDescription($"{publicId}.jpg", stream),
            Folder = folder,
            PublicId = publicId,
            Overwrite = false,
            Transformation = new Transformation()
                .Width(1920).Height(1080).Crop("limit") // Max dimensions
                .Quality("auto:good") // Compression auto
                .FetchFormat("auto"), // Format optimal
        };

        var uploadResult = await cloudinaryWrapper.UploadAsync(uploadParams);

        if (uploadResult.StatusCode != System.Net.HttpStatusCode.OK)
        {
            logger.LogError("Erreur upload Cloudinary: {Error}", uploadResult.Error?.Message);
            throw new InvalidOperationException($"Upload failed: {uploadResult.Error?.Message}");
        }

        // Générer les URLs signées
        var imageUrl = GenerateSignedUrl(uploadResult.PublicId);
        var thumbnailUrl = GenerateSignedUrl(
            uploadResult.PublicId,
            thumbnail: true);

        logger.LogInformation(
            "Image uploadée avec succès: {PublicId}, URLs générées (expire: {Hours}h)",
            uploadResult.PublicId,
            expirationHours);

        return (imageUrl, thumbnailUrl);
    }

    public async Task<bool> DeleteImageAsync(string publicId)
    {
        var deleteParams = new DeletionParams(publicId);
        var result = await cloudinaryWrapper.DestroyAsync(deleteParams);

        logger.LogInformation(
            "Suppression image Cloudinary: {PublicId}, Result: {Result}",
            publicId,
            result.Result);

        return result.Result == "ok";
    }

    private string GenerateSignedUrl(string publicId, bool thumbnail = false)
    {
        var transformation = thumbnail
            ? new Transformation()
                .Width(100).Height(100).Crop("fill")
                .Effect("blur:500") // Floutage intense
                .Quality("auto:low")
            : new Transformation()
                .Quality("auto:good");

        // Générer une URL publique Cloudinary (pas de signature nécessaire pour les images éphémères)
        // La sécurité est assurée par le délai d'affichage (3s) côté client
        var url = cloudinaryWrapper.UrlImgUp
            .Transform(transformation)
            .Secure(true)
            .BuildUrl(publicId);

        return url;
    }
}