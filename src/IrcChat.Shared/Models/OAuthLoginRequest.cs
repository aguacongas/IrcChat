namespace IrcChat.Shared.Models;

public class OAuthLoginRequest
{
    public ExternalAuthProvider Provider { get; set; }
    public string AccessToken { get; set; } = string.Empty;
}
