
using Microsoft.AspNetCore.Components.Forms;
namespace IrcChat.Client.Services;
/// <summary>
/// Service de gestion des photos éphémères côté client.
/// </summary>
public interface IEphemeralPhotoService : IAsyncDisposable
{
    /// <summary>
    /// Valide un fichier image (taille, format).
    /// </summary>
    Task<bool> ValidateImageFileAsync(IBrowserFile file);
    /// <summary>
    /// Upload une image vers le backend (qui upload vers Cloudinary).
    /// </summary>
    /// <param name="imageBase64">Image en base64.</param>
    /// <param name="userId">ID de l'utilisateur.</param>
    /// <returns>Tuple (ImageUrl, ThumbnailUrl).</returns>
    Task<(string ImageUrl, string ThumbnailUrl)> UploadImageAsync(string imageBase64, string userId);

    /// <summary>
    /// Démarre la caméra et retourne true si succès.
    /// </summary>
    Task<bool> StartCameraAsync();

    /// <summary>
    /// Arrête la caméra.
    /// </summary>
    Task StopCameraAsync();

    /// <summary>
    /// Attache le stream de la caméra à un élément video.
    /// </summary>
    Task AttachCameraToVideoAsync(string videoElementId);

    /// <summary>
    /// Capture une photo depuis l'élément video et retourne le base64.
    /// </summary>
    Task<string> CapturePhotoAsync(string videoElementId);

    /// <summary>
    /// Vérifie si la caméra est disponible sur l'appareil.
    /// </summary>
    Task<bool> IsCameraAvailableAsync();

    /// <summary>
    /// Bloque les DevTools (sécurité photo éphémère).
    /// </summary>
    Task BlockDevToolsAsync();

    /// <summary>
    /// Détecte les tentatives de screenshot.
    /// </summary>
    Task DetectScreenshotAsync();

    /// <summary>
    /// Détruit les données de l'image de la mémoire.
    /// </summary>
    Task DestroyImageDataAsync(string elementId);
}