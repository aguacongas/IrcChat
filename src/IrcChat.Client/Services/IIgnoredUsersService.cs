namespace IrcChat.Client.Services;

/// <summary>
/// Interface pour la gestion des utilisateurs ignorés.
/// Permet de persister et récupérer les utilisateurs ignorés via IndexedDB.
/// </summary>
public interface IIgnoredUsersService
{
    /// <summary>
    /// Initialize le service
    /// </summary>
    /// <returns></returns>
    Task InitializeAsync();

    /// <summary>
    /// Vérifie si un utilisateur est ignoré par son ID.
    /// </summary>
    bool IsUserIgnored(string userId);

    /// <summary>
    /// Ajoute un utilisateur à la liste des ignorés.
    /// </summary>
    Task IgnoreUserAsync(string userId);

    /// <summary>
    /// Retire un utilisateur de la liste des ignorés.
    /// </summary>
    Task UnignoreUserAsync(string userId);

    /// <summary>
    /// Bascule le statut d'ignorance d'un utilisateur.
    /// </summary>
    Task ToggleIgnoreUserAsync(string userId);

    /// <summary>
    /// Événement déclenché quand la liste des ignorés change.
    /// </summary>
    event Action? OnIgnoredUsersChanged;
}