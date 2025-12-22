namespace IrcChat.Api.Services;

/// <summary>
/// Service de gestion des photos éphémères côté serveur.
/// Validation, compression, génération de thumbnails, rate limiting.
/// </summary>
public interface IEphemeralPhotoService
{
    /// <summary>
    /// Valide une image (format, taille).
    /// </summary>
    /// <param name="imageBytes">Image à valider.</param>
    /// <param name="maxSizeKb">Taille maximale en KB.</param>
    /// <returns>True si valide, False sinon.</returns>
    Task<bool> ValidateImageAsync(byte[] imageBytes, int maxSizeKb);

    /// <summary>
    /// Génère une miniature floutée pour preview.
    /// </summary>
    /// <param name="imageBytes">Image originale.</param>
    /// <returns>Thumbnail floutée en Base64.</returns>
    Task<byte[]> GenerateBlurredThumbnailAsync(byte[] imageBytes);

    /// <summary>
    /// Compresse une image JPEG/PNG.
    /// </summary>
    /// <param name="imageBytes">Image originale.</param>
    /// <param name="quality">Qualité de compression (0-100).</param>
    /// <returns>Image compressée en Base64.</returns>
    Task<byte[]> CompressImageAsync(byte[] imageBytes, int quality);

    /// <summary>
    /// Vérifie si l'utilisateur respecte le rate limit (5 photos/minute).
    /// </summary>
    /// <param name="userId">ID de l'utilisateur.</param>
    /// <returns>True si autorisé, False si rate limit dépassé.</returns>
    bool CheckRateLimit(string userId);

    /// <summary>
    /// Enregistre l'envoi d'une photo pour rate limiting.
    /// </summary>
    /// <param name="userId">ID de l'utilisateur.</param>
    void RecordPhotoSent(string userId);
}