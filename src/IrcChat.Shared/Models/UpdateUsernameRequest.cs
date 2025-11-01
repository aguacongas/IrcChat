namespace IrcChat.Shared.Models;

public class UpdateUsernameRequest
{
    public Guid UserId { get; set; }
    public string NewUsername { get; set; } = string.Empty;
}