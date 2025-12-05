using IrcChat.Shared.Models;

namespace IrcChat.Client.Services;

public interface IOAuthClientService
{
    Task<string> InitiateAuthorizationFlowAsync(ExternalAuthProvider provider, string redirectUri);

    Task<OAuthLoginResponse?> HandleCallbackAsync(string code, string state, string redirectUri);
}