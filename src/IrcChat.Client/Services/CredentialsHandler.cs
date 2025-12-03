
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Components.WebAssembly.Http;

namespace IrcChat.Client.Services;

public class CredentialsHandler(IUnifiedAuthService unifiedAuthService) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        await unifiedAuthService.InitializeAsync();
        await unifiedAuthService.SetClientCookieAsync();
        if (unifiedAuthService.IsAuthenticated && unifiedAuthService.Token != null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", unifiedAuthService.Token);
        }
        request.SetBrowserRequestCredentials(BrowserRequestCredentials.Include);
        return await base.SendAsync(request, cancellationToken);
    }
}
