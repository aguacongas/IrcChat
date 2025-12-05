namespace IrcChat.Client.Services;

/// <summary>
/// Interface pour la gestion des utilisateurs ignorés.
/// Permet de persister et récupérer les utilisateurs ignorés via IndexedDB.
/// </summary>
public interface IIgnoredUsersService
{
    /// <summary>
    /// Événement déclenché quand la liste des ignorés change.
    /// </summary>
    event Action? OnIgnoredUsersChanged;

    /// <summary>
    /// Vérifie si un utilisateur est ignoré par son ID.
    /// </summary>
    /// <param name="userId">ID de l'utilisateur à vérifier.</param>
    /// <returns>Vrai si l'utilisateur est ignoré sinon faux.</returns>
    bool IsUserIgnored(string userId);

    /// <summary>
    /// Initialize le service.
    /// </summary>
    /// <returns>Tache asynchrone.</returns>
    Task InitializeAsync();

    /// <summary>
    /// Ajoute un utilisateur à la liste des ignorés.
    /// </summary>
    /// <param name="userId">ID de l'utilisateur à ignorer.</param>
    /// <returns>Tache asynchrone.</returns>
    Task IgnoreUserAsync(string userId);

    /// <summary>
    /// Retire un utilisateur de la liste des ignorés.
    /// </summary>
    /// <param name="userId">ID de l'utilisateur à ignorer.</param>
    /// <returns>Tache asynchrone.</returns>
    Task UnignoreUserAsync(string userId);

    /// <summary>
    /// Bascule le statut d'ignorance d'un utilisateur.
    /// </summary>
    /// <param name="userId">ID de l'utilisateur à ignorer.</param>
    /// <returns>Tache asynchrone.</returns>
    Task ToggleIgnoreUserAsync(string userId);
}