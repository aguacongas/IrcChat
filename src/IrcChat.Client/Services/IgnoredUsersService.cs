using Microsoft.JSInterop;

namespace IrcChat.Client.Services;

/// <summary>
/// Service de gestion des utilisateurs ignorés côté client.
/// Utilise IndexedDB pour la persistance, via IJSRuntime et userIdManager.js.
/// </summary>
public class IgnoredUsersService(IJSRuntime jsRuntime, ILogger<IgnoredUsersService> logger) : IIgnoredUsersService, IAsyncDisposable
{
    private bool isInitialized = false;
    private IJSObjectReference? module;
    private List<string> ignoredUsersCache = [];

    public event Action? OnIgnoredUsersChanged;

    /// <summary>
    /// Initialise le service en chargeant le module JavaScript.
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    public async Task InitializeAsync()
    {
        if (isInitialized)
        {
            return;
        }

        try
        {
            module = await jsRuntime.InvokeAsync<IJSObjectReference>(
                "import", "./js/ignoredUsersManager.js");
            logger.LogInformation("Module ignoredUsersManager.js chargé avec succès");
            ignoredUsersCache = await module.InvokeAsync<List<string>>("getAllIgnoredUsers");
            isInitialized = true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Erreur lors du chargement du module ignoredUsersManager.js");
        }
    }

    /// <summary>
    /// Vérifie si un utilisateur est ignoré par son ID.
    /// </summary>
    /// <returns></returns>
    public bool IsUserIgnored(string userId)
    {
        if (!isInitialized)
        {
            logger.LogWarning("Module non initialisé, impossible de vérifier si l'utilisateur est ignoré");
            return false;
        }

        return ignoredUsersCache.Contains(userId);
    }

    /// <summary>
    /// Ajoute un utilisateur à la liste des ignorés.
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    public async Task IgnoreUserAsync(string userId)
    {
        if (!isInitialized)
        {
            logger.LogWarning("Module non initialisé, impossible d'ignorer l'utilisateur");
            return;
        }

        try
        {
            await module!.InvokeVoidAsync("ignoreUser", userId);
            ignoredUsersCache.Add(userId);
            logger.LogInformation("Utilisateur {UserId} ignoré", userId);
            OnIgnoredUsersChanged?.Invoke();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Erreur lors de l'ignorance de l'utilisateur {UserId}", userId);
        }
    }

    /// <summary>
    /// Retire un utilisateur de la liste des ignorés.
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    public async Task UnignoreUserAsync(string userId)
    {
        if (!isInitialized)
        {
            logger.LogWarning("Module non initialisé, impossible de dés-ignorer l'utilisateur");
            return;
        }

        try
        {
            await module!.InvokeVoidAsync("unignoreUser", userId);
            ignoredUsersCache.Remove(userId);
            logger.LogInformation("Utilisateur {UserId} dés-ignoré", userId);
            OnIgnoredUsersChanged?.Invoke();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Erreur lors de la dés-ignorance de l'utilisateur {UserId}", userId);
        }
    }

    /// <summary>
    /// Bascule le statut d'ignorance d'un utilisateur.
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    public async Task ToggleIgnoreUserAsync(string userId)
    {
        var isIgnored = IsUserIgnored(userId);

        if (isIgnored)
        {
            await UnignoreUserAsync(userId);
        }
        else
        {
            await IgnoreUserAsync(userId);
        }
    }

    /// <summary>
    /// Nettoie les ressources du module JavaScript.
    /// </summary>
    /// <returns></returns>
    public async ValueTask DisposeAsync()
    {
        if (module is not null)
        {
            try
            {
                await module.DisposeAsync();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Erreur lors du dispose du module ignoredUsersManager.js");
            }
        }

        GC.SuppressFinalize(this);
    }
}