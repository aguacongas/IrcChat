// src/IrcChat.Shared/Models/OAuthLoginResponse.cs
namespace IrcChat.Shared.Models;

public class OAuthLoginResponse
{
    public string Token { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public Guid UserId { get; set; }
    public bool IsNewUser { get; set; }
    public bool IsAdmin { get; set; }
}