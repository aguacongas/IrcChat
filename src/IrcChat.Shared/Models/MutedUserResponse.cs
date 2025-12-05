namespace IrcChat.Shared.Models;

/// <summary>
/// Représente les informations d'un utilisateur mué retournées par l'API.
/// </summary>
public class MutedUserResponse
{
    /// <summary>
    /// Gets or sets userId de l'utilisateur mué.
    /// </summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets nom d'utilisateur de la personne mutée.
    /// </summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets uRL de l'avatar de l'utilisateur (si disponible).
    /// </summary>
    public string? AvatarUrl { get; set; }

    /// <summary>
    /// Gets or sets userId de la personne qui a effectué le mute.
    /// </summary>
    public string MutedByUserId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets nom d'utilisateur de la personne qui a effectué le mute.
    /// </summary>
    public string MutedByUsername { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets date et heure du mute.
    /// </summary>
    public DateTime MutedAt { get; set; }

    /// <summary>
    /// Gets or sets raison optionnelle du mute.
    /// </summary>
    public string? Reason { get; set; }
}