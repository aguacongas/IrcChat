namespace IrcChat.Shared.Models;

public class ReserveUsernameRequest
{
    public string Username { get; set; } = string.Empty;
    public ExternalAuthProvider Provider { get; set; }
    public string Code { get; set; } = string.Empty;
    public string RedirectUri { get; set; } = string.Empty;
    public string CodeVerifier { get; set; } = string.Empty;
}