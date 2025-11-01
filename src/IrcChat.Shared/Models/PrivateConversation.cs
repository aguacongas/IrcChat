namespace IrcChat.Shared.Models;

public class PrivateConversation
{
    public string OtherUsername { get; set; } = string.Empty;
    public string? LastMessage { get; set; }
    public DateTime? LastMessageTime { get; set; }
    public int UnreadCount { get; set; }
}