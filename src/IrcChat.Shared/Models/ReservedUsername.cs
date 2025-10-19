namespace IrcChat.Shared.Models;

public class ReservedUsername
{
    public Guid Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public ExternalAuthProvider Provider { get; set; }
    public string ExternalUserId { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public string? AvatarUrl { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime LastLoginAt { get; set; }
}
