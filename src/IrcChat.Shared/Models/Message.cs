namespace IrcChat.Shared.Models;

public class Message
{
    public Guid Id { get; set; }

    public string Username { get; set; } = string.Empty;

    public string Content { get; set; } = string.Empty;

    public string Channel { get; set; } = string.Empty;

    public DateTime Timestamp { get; set; }

    public bool IsDeleted { get; set; }

    public string UserId { get; set; } = string.Empty;
}