namespace IrcChat.Shared.Models;

/// <summary>
/// Représente un utilisateur mué dans un salon spécifique ou globalement
/// Quand ChannelName est null, l'utilisateur est mué globalement sur tous les salons
/// </summary>
public class MutedUser
{
    public Guid Id { get; set; }

    /// <summary>
    /// Nom du salon où l'utilisateur est mué, ou null si mué globalement
    /// </summary>
    public string? ChannelName { get; set; }

    /// <summary>
    /// UserId de la personne mutée (identifiant unique permanent)
    /// </summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// UserId de la personne qui a effectué le mute
    /// </summary>
    public string MutedByUserId { get; set; } = string.Empty;

    /// <summary>
    /// Date et heure du mute
    /// </summary>
    public DateTime MutedAt { get; set; }

    /// <summary>
    /// Raison optionnelle du mute
    /// </summary>
    public string? Reason { get; set; }
}