using IrcChat.Shared.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;

namespace IrcChat.Client.Services;

public class OAuthClientService(
    IJSRuntime jsRuntime,
    HttpClient httpClient,
    NavigationManager navigationManager)
{
    public async Task<string> InitiateAuthorizationFlowAsync(ExternalAuthProvider provider, string redirectUri)
    {
        // Obtenir la configuration du provider
        var response = await httpClient.GetFromJsonAsync<OAuthProviderConfig>(
            $"/api/oauth/config/{provider}");

        if (response == null)
            throw new Exception("Failed to get OAuth configuration");

        // Générer state et code_verifier pour PKCE
        var state = GenerateRandomString(32);
        var codeVerifier = GenerateRandomString(128);
        var codeChallenge = GenerateCodeChallenge(codeVerifier);

        // Sauvegarder dans le sessionStorage
        await jsRuntime.InvokeVoidAsync("sessionStorage.setItem", "oauth_state", state);
        await jsRuntime.InvokeVoidAsync("sessionStorage.setItem", "oauth_code_verifier", codeVerifier);
        await jsRuntime.InvokeVoidAsync("sessionStorage.setItem", "oauth_provider", provider.ToString());

        // Construire l'URL d'autorisation
        var authUrl = $"{response.AuthorizationEndpoint}" +
            $"?client_id={Uri.EscapeDataString(response.ClientId)}" +
            $"&redirect_uri={Uri.EscapeDataString(redirectUri)}" +
            $"&response_type=code" +
            $"&scope={Uri.EscapeDataString(response.Scope)}" +
            $"&state={state}" +
            $"&code_challenge={codeChallenge}" +
            $"&code_challenge_method=S256";

        return authUrl;
    }

    public async Task<OAuthLoginResponse?> HandleCallbackAsync(string code, string state, string redirectUri)
    {
        // Vérifier le state
        var savedState = await jsRuntime.InvokeAsync<string>("sessionStorage.getItem", "oauth_state");
        if (state != savedState)
        {
            throw new Exception("Invalid state parameter - possible CSRF attack");
        }

        // Récupérer le provider et code_verifier
        var providerStr = await jsRuntime.InvokeAsync<string>("sessionStorage.getItem", "oauth_provider");
        var codeVerifier = await jsRuntime.InvokeAsync<string>("sessionStorage.getItem", "oauth_code_verifier");

        if (!Enum.TryParse<ExternalAuthProvider>(providerStr, out var provider))
        {
            throw new Exception("Invalid provider");
        }

        // Échanger le code contre un token via notre API (avec code_verifier)
        var tokenRequest = new OAuthTokenRequest
        {
            Provider = provider,
            Code = code,
            RedirectUri = redirectUri,
            CodeVerifier = codeVerifier // AJOUT du code_verifier
        };

        var response = await httpClient.PostAsJsonAsync("/api/oauth/token", tokenRequest);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            throw new Exception($"Token exchange failed: {error}");
        }

        var result = await response.Content.ReadFromJsonAsync<OAuthLoginResponse>();

        // Nettoyer le sessionStorage
        await jsRuntime.InvokeVoidAsync("sessionStorage.removeItem", "oauth_state");
        await jsRuntime.InvokeVoidAsync("sessionStorage.removeItem", "oauth_code_verifier");
        await jsRuntime.InvokeVoidAsync("sessionStorage.removeItem", "oauth_provider");

        return result;
    }

    private string GenerateRandomString(int length)
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-._~";
        var random = new byte[length];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(random);
        }
        return new string(random.Select(b => chars[b % chars.Length]).ToArray());
    }

    private string GenerateCodeChallenge(string codeVerifier)
    {
        using var sha256 = SHA256.Create();
        var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(codeVerifier));
        return Convert.ToBase64String(hash)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }
}

public class OAuthProviderConfig
{
    public ExternalAuthProvider Provider { get; set; }
    public string AuthorizationEndpoint { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string Scope { get; set; } = string.Empty;
}
