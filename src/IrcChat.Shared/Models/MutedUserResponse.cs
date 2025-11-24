namespace IrcChat.Shared.Models;

/// <summary>
/// Représente les informations d'un utilisateur mué retournées par l'API
/// </summary>
public class MutedUserResponse
{
    /// <summary>
    /// UserId de l'utilisateur mué
    /// </summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// Nom d'utilisateur de la personne mutée
    /// </summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// URL de l'avatar de l'utilisateur (si disponible)
    /// </summary>
    public string? AvatarUrl { get; set; }

    /// <summary>
    /// UserId de la personne qui a effectué le mute
    /// </summary>
    public string MutedByUserId { get; set; } = string.Empty;

    /// <summary>
    /// Nom d'utilisateur de la personne qui a effectué le mute
    /// </summary>
    public string MutedByUsername { get; set; } = string.Empty;

    /// <summary>
    /// Date et heure du mute
    /// </summary>
    public DateTime MutedAt { get; set; }

    /// <summary>
    /// Raison optionnelle du mute
    /// </summary>
    public string? Reason { get; set; }
}