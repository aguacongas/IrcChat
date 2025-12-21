namespace IrcChat.Api.Services;
/// <summary>
/// Service de gestion des uploads vers Cloudinary.
/// </summary>
public interface ICloudinaryService
{
    /// <summary>
    /// Upload une image vers Cloudinary et retourne les URLs (full + thumbnail).
    /// </summary>
    /// <param name="imageBytes">Données de l'image.</param>
    /// <param name="userId">ID de l'utilisateur (pour le dossier).</param>
    /// <returns>Tuple (ImageUrl, ThumbnailUrl).</returns>
    Task<(string ImageUrl, string ThumbnailUrl)> UploadEphemeralPhotoAsync(byte[] imageBytes, string userId);
    /// <summary>
    /// Supprime une image de Cloudinary (optionnel, Cloudinary auto-expire les signed URLs).
    /// </summary>
    /// <param name="publicId">ID public de l'image.</param>
    Task<bool> DeleteImageAsync(string publicId);
}