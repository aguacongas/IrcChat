namespace IrcChat.Shared.Models;

/// <summary>
/// Représente un utilisateur mué dans un salon spécifique
/// </summary>
public class MutedUser
{
    public Guid Id { get; set; }

    /// <summary>
    /// Nom du salon où l'utilisateur est mué
    /// </summary>
    public string ChannelName { get; set; } = string.Empty;

    /// <summary>
    /// Nom d'utilisateur de la personne mutée
    /// </summary>
    public string? Username { get; set; }

    /// <summary>
    /// UserId de la personne mutée
    /// </summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// Nom d'utilisateur de la personne qui a effectué le mute
    /// </summary>
    public string MutedBy { get; set; } = string.Empty;

    /// <summary>
    /// Date et heure du mute
    /// </summary>
    public DateTime MutedAt { get; set; }

    /// <summary>
    /// Raison optionnelle du mute
    /// </summary>
    public string? Reason { get; set; }
}