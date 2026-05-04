namespace IrcChat.Shared.Models;

/// <summary>
/// DTO représentant les réactions agrégées par emoji pour un message.
/// </summary>
public class MessageReactionDto
{
    public string Emoji { get; set; } = string.Empty;

    public int Count { get; set; }

    public List<string> UserIds { get; set; } = [];

    public List<string> Usernames { get; set; } = [];
}