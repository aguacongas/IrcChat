namespace IrcChat.Shared.Models;

public class PrivateMessage
{
    public Guid Id { get; set; }
    public string SenderUsername { get; set; } = string.Empty;
    public string RecipientUsername { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public bool IsRead { get; set; }
    public bool IsDeleted { get; set; }
}