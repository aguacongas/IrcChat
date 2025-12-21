using System.Net.Http.Json;
using IrcChat.Shared.Models;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.JSInterop;
namespace IrcChat.Client.Services;
/// <summary>
/// Implémentation du service de photos éphémères côté client.
/// </summary>
public class EphemeralPhotoService(
HttpClient httpClient,
ILogger<EphemeralPhotoService> logger,
IJSRuntime jsRuntime) : IEphemeralPhotoService
{
    private static readonly long MaxFileSizeBytes = 2 * 1024 * 1024; // 2MB
    private static readonly string[] AllowedMimeTypes = ["image/jpeg", "image/png", "image/webp"];
    private IJSObjectReference? _cameraModule;
    private IJSObjectReference? _ephemeralModule;
    public async Task<bool> ValidateImageFileAsync(IBrowserFile file)
    {
        logger.LogInformation("Validation du fichier: {FileName}, {Size} bytes, {ContentType}",
            file.Name, file.Size, file.ContentType);

        // Vérifier le type MIME
        if (!AllowedMimeTypes.Contains(file.ContentType.ToLowerInvariant()))
        {
            logger.LogWarning("Type de fichier non supporté: {ContentType}", file.ContentType);
            return false;
        }

        // Vérifier la taille
        if (file.Size > MaxFileSizeBytes)
        {
            logger.LogWarning("Fichier trop volumineux: {Size} bytes (max: {Max} bytes)",
                file.Size, MaxFileSizeBytes);
            return false;
        }

        await Task.CompletedTask;
        return true;
    }

    public async Task<(string ImageUrl, string ThumbnailUrl)> UploadImageAsync(string imageBase64, string userId)
    {
        try
        {
            logger.LogInformation("Upload image pour UserId {UserId}", userId);

            // Retirer le préfixe data:image/...;base64, si présent
            var base64Data = imageBase64.Contains(',')
                ? imageBase64.Split(',')[1]
                : imageBase64;

            var request = new UploadEphemeralPhotoRequest
            {
                ImageBase64 = base64Data
            };

            var response = await httpClient.PostAsJsonAsync(
                $"/api/ephemeral-photos/{userId}/upload",
                request);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                logger.LogError("Erreur upload: {StatusCode}, {Error}", response.StatusCode, error);
                throw new HttpRequestException($"Upload failed: {response.StatusCode}");
            }

            var result = await response.Content.ReadFromJsonAsync<UploadEphemeralPhotoResponse>();

            if (result == null)
            {
                throw new InvalidOperationException("Réponse invalide du serveur");
            }

            logger.LogInformation("Image uploadée avec succès");
            return (result.ImageUrl, result.ThumbnailUrl);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Erreur lors de l'upload de l'image");
            throw;
        }
    }

    // ========== Méthodes Caméra ==========

    public async Task<bool> StartCameraAsync()
    {
        try
        {
            var module = await GetCameraModuleAsync();
            await module.InvokeVoidAsync("startCamera");
            logger.LogInformation("Caméra démarrée");
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Erreur lors du démarrage de la caméra");
            return false;
        }
    }

    public async Task StopCameraAsync()
    {
        try
        {
            var module = await GetCameraModuleAsync();
            await module.InvokeVoidAsync("stopCamera");
            logger.LogInformation("Caméra arrêtée");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Erreur lors de l'arrêt de la caméra");
        }
    }

    public async Task AttachCameraToVideoAsync(string videoElementId)
    {
        try
        {
            var module = await GetCameraModuleAsync();
            var stream = await module.InvokeAsync<IJSObjectReference>("startCamera");
            await module.InvokeVoidAsync("attachStreamToVideo", videoElementId, stream);
            logger.LogInformation("Stream caméra attaché à {ElementId}", videoElementId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Erreur lors de l'attachement du stream caméra");
            throw;
        }
    }

    public async Task<string> CapturePhotoAsync(string videoElementId)
    {
        try
        {
            var module = await GetCameraModuleAsync();
            var base64 = await module.InvokeAsync<string>("capturePhotoFromVideo", videoElementId);
            logger.LogInformation("Photo capturée depuis {ElementId}", videoElementId);
            return base64;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Erreur lors de la capture de la photo");
            throw;
        }
    }

    public async Task<bool> IsCameraAvailableAsync()
    {
        try
        {
            var module = await GetCameraModuleAsync();
            return await module.InvokeAsync<bool>("isCameraAvailable");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Erreur lors de la vérification de disponibilité caméra");
            return false;
        }
    }

    // ========== Méthodes Sécurité (Ephemeral) ==========

    public async Task BlockDevToolsAsync()
    {
        try
        {
            var module = await GetEphemeralModuleAsync();
            await module.InvokeVoidAsync("blockDevTools");
            logger.LogInformation("Blocage DevTools activé");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Erreur lors du blocage DevTools");
        }
    }

    public async Task DetectScreenshotAsync()
    {
        try
        {
            var module = await GetEphemeralModuleAsync();
            await module.InvokeVoidAsync("detectScreenshot");
            logger.LogInformation("Détection screenshot activée");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Erreur lors de la détection screenshot");
        }
    }

    public async Task DestroyImageDataAsync(string elementId)
    {
        try
        {
            var module = await GetEphemeralModuleAsync();
            await module.InvokeVoidAsync("destroyImageData", elementId);
            logger.LogInformation("Image {ElementId} détruite de la mémoire", elementId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Erreur lors de la destruction de l'image");
        }
    }

    // ========== Gestion des modules JS ==========

    private async Task<IJSObjectReference> GetCameraModuleAsync()
    {
        if (_cameraModule == null)
        {
            try
            {
                _cameraModule = await jsRuntime.InvokeAsync<IJSObjectReference>("import", "./js/camera.js");
                logger.LogInformation("Module JS camera.js chargé");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Erreur lors du chargement du module camera.js");
                throw;
            }
        }
        return _cameraModule;
    }

    private async Task<IJSObjectReference> GetEphemeralModuleAsync()
    {
        if (_ephemeralModule == null)
        {
            try
            {
                _ephemeralModule = await jsRuntime.InvokeAsync<IJSObjectReference>("import", "./js/ephemeralPhoto.js");
                logger.LogInformation("Module JS ephemeralPhoto.js chargé");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Erreur lors du chargement du module ephemeralPhoto.js");
                throw;
            }
        }
        return _ephemeralModule;
    }

    public async ValueTask DisposeAsync()
    {
        if (_cameraModule != null)
        {
            try
            {
                await _cameraModule.InvokeVoidAsync("stopCamera");
                await _cameraModule.DisposeAsync();
                logger.LogInformation("Module JS camera.js disposé");
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Erreur lors du dispose du module camera.js");
            }
        }

        if (_ephemeralModule != null)
        {
            try
            {
                await _ephemeralModule.DisposeAsync();
                logger.LogInformation("Module JS ephemeralPhoto.js disposé");
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Erreur lors du dispose du module ephemeralPhoto.js");
            }
        }

        GC.SuppressFinalize(this);
    }
}