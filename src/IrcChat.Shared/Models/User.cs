namespace IrcChat.Shared.Models;

public class User
{
    public string UserId { get; set; } = string.Empty;

    public string Username { get; set; } = string.Empty;

    public DateTime ConnectedAt { get; set; }
}