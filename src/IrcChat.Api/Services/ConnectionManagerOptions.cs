namespace IrcChat.Api.Services;

public class ConnectionManagerOptions
{
    public const string SectionName = "ConnectionManager";

    /// <summary>
    /// Identifiant unique de cette instance de l'application.
    /// Si non défini, utilise le nom de la machine.
    /// </summary>
    public string? InstanceId { get; set; }

    /// <summary>
    /// Intervalle de nettoyage des connexions inactives (en secondes).
    /// Par défaut: 30 secondes.
    /// </summary>
    public int CleanupIntervalSeconds { get; set; } = 30;

    /// <summary>
    /// Délai avant qu'un utilisateur soit considéré comme déconnecté (en secondes).
    /// Par défaut: 60 secondes.
    /// </summary>
    public int UserTimeoutSeconds { get; set; } = 60;
}