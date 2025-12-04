
using System.Net.Http.Headers;

namespace IrcChat.Client.Services;

public class CredentialsHandler(IRequestAuthenticationService requestAuthenticationService) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (requestAuthenticationService.Token != null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", requestAuthenticationService.Token);
        }
        request.Headers.Add("X-ConnectionId", requestAuthenticationService.ConnectionId);
        return await base.SendAsync(request, cancellationToken);
    }
}