namespace IrcChat.Shared.Models;

public class CompleteProfileRequest
{
    public Guid TempUserId { get; set; }
    public string Username { get; set; } = string.Empty;
}