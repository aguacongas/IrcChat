namespace IrcChat.Shared.Models;

public class PrivateConversation
{
    public string? LastMessage { get; set; }

    public DateTime? LastMessageTime { get; set; }

    public int UnreadCount { get; set; }

    public bool IsOnline { get; set; }

    public User? OtherUser { get; set; }
}