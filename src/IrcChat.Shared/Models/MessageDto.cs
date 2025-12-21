namespace IrcChat.Shared.Models;

public class MessageDto
{
    public Guid Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public string Channel { get; set; } = string.Empty;

    /// <summary>
    /// Indique si ce message contient une photo éphémère.
    /// </summary>
    public bool HasEphemeralPhoto { get; set; }

    /// <summary>
    /// ID de la photo éphémère associée (si HasEphemeralPhoto = true).
    /// </summary>
    public Guid? EphemeralPhotoId { get; set; }
}