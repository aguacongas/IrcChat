using IrcChat.Shared.Models;

namespace IrcChat.Client.Models;

public class OAuthProviderConfig
{
    public ExternalAuthProvider Provider { get; set; }

    public string AuthorizationEndpoint { get; set; } = string.Empty;

    public string ClientId { get; set; } = string.Empty;

    public string Scope { get; set; } = string.Empty;
}