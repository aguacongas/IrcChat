// src/IrcChat.Client/Services/DeviceDetectorService.cs
using System.Diagnostics.CodeAnalysis;
using Microsoft.JSInterop;

namespace IrcChat.Client.Services;

public class DeviceDetectorService(IJSRuntime jsRuntime, ILogger<DeviceDetectorService> logger) : IDeviceDetectorService, IAsyncDisposable
{
    private IJSObjectReference? module;
    private bool isInitialized;

    public async Task<bool> IsMobileDeviceAsync()
    {
        await EnsureInitializedAsync();

        try
        {
            return await module!.InvokeAsync<bool>("isMobileDevice");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Erreur lors de la détection du type d'appareil");
            return false;
        }
    }

    public async Task<int> GetScreenWidthAsync()
    {
        await EnsureInitializedAsync();

        try
        {
            return await module!.InvokeAsync<int>("getScreenWidth");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Erreur lors de la récupération de la largeur d'écran");
            return 1024; // Valeur par défaut (desktop)
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (module != null)
        {
            try
            {
                await module.DisposeAsync();
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Erreur lors de la libération du module de détection d'appareil");
            }
        }

        GC.SuppressFinalize(this);
    }

    [SuppressMessage("Major Code Smell", "S2139:Exceptions should be either logged or rethrown but not both", Justification = "It's logged")]
    private async Task EnsureInitializedAsync()
    {
        if (isInitialized)
        {
            return;
        }

        try
        {
            module = await jsRuntime.InvokeAsync<IJSObjectReference>(
                "import", "./js/device-detector.js");
            isInitialized = true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Erreur lors du chargement du module de détection d'appareil");
            throw;
        }
    }
}