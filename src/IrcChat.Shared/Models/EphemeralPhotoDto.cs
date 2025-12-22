namespace IrcChat.Shared.Models;

/// <summary>
/// DTO pour les photos éphémères (3 secondes d'affichage).
/// Pas de stockage serveur - transit via SignalR uniquement.
/// </summary>
public class EphemeralPhotoDto : IMessage
{
    /// <summary>
    /// Identifiant unique de la photo éphémère.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// ID de l'expéditeur.
    /// </summary>
    public required string SenderId { get; set; }

    /// <summary>
    /// Nom d'utilisateur de l'expéditeur.
    /// </summary>
    public string SenderUsername { get; set; } = string.Empty;

    /// <summary>
    /// ID du canal (null si message privé).
    /// </summary>
    public string? ChannelId { get; set; }

    /// <summary>
    /// ID du destinataire (null si canal public).
    /// </summary>
    public string? RecipientId { get; set; }

    /// <summary>
    /// URL de l'image full-size (Cloudinary signed URL).
    /// </summary>
    public string? ImageUrl { get; set; }

    /// <summary>
    /// URL de la thumbnail floutée (Cloudinary transformation).
    /// </summary>
    public required string ThumbnailUrl { get; set; }

    /// <summary>
    /// Date/heure d'envoi.
    /// </summary>
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// Date/heure d'expiration (SentAt + 3 secondes).
    /// </summary>
    public DateTime ExpiresAt { get; set; }
}