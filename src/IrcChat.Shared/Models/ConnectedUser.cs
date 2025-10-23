namespace IrcChat.Shared.Models;

public class ConnectedUser
{
    public Guid Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string ConnectionId { get; set; } = string.Empty;
    public string Channel { get; set; } = string.Empty;
    public DateTime ConnectedAt { get; set; }
    public DateTime LastActivity { get; set; }
    public DateTime LastPing { get; set; }
    public string ServerInstanceId { get; set; } = string.Empty;
}
