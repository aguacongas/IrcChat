// src/IrcChat.Client/Services/NotificationSoundService.cs
using System.Diagnostics.CodeAnalysis;
using Microsoft.JSInterop;

namespace IrcChat.Client.Services;

/// <summary>
/// Implémentation du service de notifications sonores.
/// Gère le stockage des préférences et l'interop JavaScript pour jouer les sons.
/// Importe dynamiquement le module JS audioPlayer.
/// </summary>
public class NotificationSoundService(IJSRuntime jsRuntime, ILogger<NotificationSoundService> logger) : INotificationSoundService, IAsyncDisposable
{
    private static readonly string LocalStorageKey = "notification-sound-enabled";
    private static readonly string ModulePath = "./js/audioPlayer.js";
    private static readonly string PlaySoundMethod = "playSound";

    private IJSObjectReference? _module;
    private bool _moduleLoadFailed;

    /// <inheritdoc/>
    public async Task PlaySoundAsync()
    {
        try
        {
            var isEnabled = await IsSoundEnabledAsync();
            if (!isEnabled)
            {
                return;
            }

            var module = await GetModuleAsync();
            if (module is null)
            {
                return;
            }

            await module.InvokeVoidAsync(PlaySoundMethod);
        }
        catch (Exception ex)
        {
            // Fallback silencieux : on log mais on ne throw pas
            logger.LogWarning(ex, "Erreur lors de la lecture du son de notification, ignorée");
        }
    }

    /// <inheritdoc/>
    [SuppressMessage("Major Code Smell", "S2139:Exceptions should be either logged or rethrown but not both", Justification = "False positive, it's logger")]
    public async Task ToggleSoundAsync()
    {
        try
        {
            var currentState = await IsSoundEnabledAsync();
            var newState = !currentState;

            await jsRuntime.InvokeVoidAsync("localStorage.setItem", LocalStorageKey, newState.ToString().ToLowerInvariant());

            logger.LogInformation("Sons de notification {State}", newState ? "activés" : "désactivés");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Erreur lors du changement d'état des notifications sonores");
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<bool> IsSoundEnabledAsync()
    {
        try
        {
            var storedValue = await jsRuntime.InvokeAsync<string?>("localStorage.getItem", LocalStorageKey);

            // Par défaut, les sons sont activés
            if (string.IsNullOrEmpty(storedValue))
            {
                return true;
            }

            return bool.TryParse(storedValue, out var result) && result;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Erreur lors de la lecture de la préférence des sons, utilisation de la valeur par défaut (activé)");
            return true; // Défaut : activé
        }
    }

    /// <summary>
    /// Dispose le module JS chargé.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (_module is not null)
        {
            try
            {
                await _module.DisposeAsync();
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Erreur lors du dispose du module audioPlayer, ignorée");
            }
        }

        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Charge le module JS audioPlayer de manière lazy.
    /// </summary>
    private async Task<IJSObjectReference?> GetModuleAsync()
    {
        if (_module is not null)
        {
            return _module;
        }

        if (_moduleLoadFailed)
        {
            return null;
        }

        try
        {
            _module = await jsRuntime.InvokeAsync<IJSObjectReference>("import", ModulePath);
            return _module;
        }
        catch (Exception ex)
        {
            _moduleLoadFailed = true;
            logger.LogWarning(ex, "Impossible de charger le module audioPlayer, notifications sonores désactivées");
            return null;
        }
    }
}