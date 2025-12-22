using System.Diagnostics.CodeAnalysis;
using IrcChat.Api.Authorization;
using IrcChat.Api.Services;
using IrcChat.Shared.Models;
using Microsoft.AspNetCore.Mvc;
namespace IrcChat.Api.Endpoints;

[SuppressMessage("Performance", "CA1873:Avoid potentially expensive logging", Justification = "Acceptable")]
public static class EphemeralPhotoEndpoints
{
    public static WebApplication MapEphemeralPhotoEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/ephemeral-photos")
            .WithTags("Ephemeral Photos");

        group.MapPost("/{userId}/upload", UploadEphemeralPhotoAsync)
            .RequireAuthorization(AuthorizationPolicies.UserIdMatch)
            .WithName("UploadEphemeralPhoto")
            .DisableAntiforgery(); // Nécessaire pour les uploads de fichiers

        return app;
    }

    private static async Task<IResult> UploadEphemeralPhotoAsync(
        string userId,
        [FromBody] UploadEphemeralPhotoRequest request,
        ICloudinaryService cloudinaryService,
        IEphemeralPhotoService ephemeralPhotoService,
        ILogger<Program> logger)
    {
        logger.LogInformation("Réception demande upload photo éphémère pour UserId {UserId}", userId);

        // Rate limiting
        if (!ephemeralPhotoService.CheckRateLimit(userId))
        {
            logger.LogWarning("Rate limit dépassé pour UserId {UserId}", userId);
            return Results.Problem(
                statusCode: 429,
                title: "Too Many Requests",
                detail: "Vous envoyez trop de photos. Attendez un peu.");
        }

        // Décoder le base64
        byte[] imageBytes;
        try
        {
            imageBytes = Convert.FromBase64String(request.ImageBase64);
        }
        catch (FormatException e)
        {
            logger.LogWarning(e, "Format base64 invalide pour UserId {UserId}", userId);
            return Results.BadRequest(new { Error = "Format base64 invalide" });
        }

        // Validation de l'image
        var maxSizeKb = 2048; // 2MB
        if (!await ephemeralPhotoService.ValidateImageAsync(imageBytes, maxSizeKb))
        {
            logger.LogWarning("Image invalide pour UserId {UserId}", userId);
            return Results.BadRequest(new { Error = "Image invalide (format ou taille non supportée)" });
        }

        try
        {
            // Upload vers Cloudinary
            var (imageUrl, thumbnailUrl) = await cloudinaryService.UploadEphemeralPhotoAsync(imageBytes, userId);

            // Enregistrer l'envoi pour rate limiting
            ephemeralPhotoService.RecordPhotoSent(userId);

            logger.LogInformation(
                "Photo éphémère uploadée avec succès pour UserId {UserId}",
                userId);

            return Results.Ok(new UploadEphemeralPhotoResponse
            {
                ImageUrl = imageUrl,
                ThumbnailUrl = thumbnailUrl
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Erreur lors de l'upload de la photo pour UserId {UserId}", userId);
            return Results.Problem(
                statusCode: 500,
                title: "Upload Error",
                detail: "Erreur lors de l'upload de l'image. Réessayez.");
        }
    }


}