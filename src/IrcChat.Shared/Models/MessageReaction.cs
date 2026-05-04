namespace IrcChat.Shared.Models;

/// <summary>
/// Représente une réaction (emoji) d'un utilisateur sur un message.
/// </summary>
public class MessageReaction
{
    public Guid Id { get; set; }

    public Guid MessageId { get; set; }

    public string UserId { get; set; } = string.Empty;

    public string Username { get; set; } = string.Empty;

    public string Emoji { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }
}