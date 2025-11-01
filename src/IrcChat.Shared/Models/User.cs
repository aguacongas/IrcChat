namespace IrcChat.Shared.Models;

public class User
{
    public string Username { get; set; } = string.Empty;
    public DateTime ConnectedAt { get; set; }
    public string? ConnectionId { get; set; }
}