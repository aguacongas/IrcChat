using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using IrcChat.Shared.Models;

namespace IrcChat.Api.Services;

public class OAuthService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<OAuthService> _logger;

    public OAuthService(HttpClient httpClient, IConfiguration configuration, ILogger<OAuthService> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;
    }

    public OAuthConfig GetProviderConfig(ExternalAuthProvider provider)
    {
        return provider switch
        {
            ExternalAuthProvider.Google => new OAuthConfig
            {
                AuthorizationEndpoint = "https://accounts.google.com/o/oauth2/v2/auth",
                TokenEndpoint = "https://oauth2.googleapis.com/token",
                UserInfoEndpoint = "https://www.googleapis.com/oauth2/v2/userinfo",
                ClientId = _configuration["OAuth:Google:ClientId"] ?? "",
                ClientSecret = _configuration["OAuth:Google:ClientSecret"] ?? "",
                Scope = "openid email profile"
            },
            ExternalAuthProvider.Facebook => new OAuthConfig
            {
                AuthorizationEndpoint = "https://www.facebook.com/v18.0/dialog/oauth",
                TokenEndpoint = "https://graph.facebook.com/v18.0/oauth/access_token",
                UserInfoEndpoint = "https://graph.facebook.com/me",
                ClientId = _configuration["OAuth:Facebook:AppId"] ?? "",
                ClientSecret = _configuration["OAuth:Facebook:AppSecret"] ?? "",
                Scope = "email public_profile"
            },
            ExternalAuthProvider.Twitter => new OAuthConfig
            {
                AuthorizationEndpoint = "https://twitter.com/i/oauth2/authorize",
                TokenEndpoint = "https://api.twitter.com/2/oauth2/token",
                UserInfoEndpoint = "https://api.twitter.com/2/users/me",
                ClientId = _configuration["OAuth:Twitter:ClientId"] ?? "",
                ClientSecret = _configuration["OAuth:Twitter:ClientSecret"] ?? "",
                Scope = "users.read tweet.read"
            },
            ExternalAuthProvider.Microsoft => new OAuthConfig
            {
                AuthorizationEndpoint = "https://login.microsoftonline.com/common/oauth2/v2.0/authorize",
                TokenEndpoint = "https://login.microsoftonline.com/common/oauth2/v2.0/token",
                UserInfoEndpoint = "https://graph.microsoft.com/v1.0/me",
                ClientId = _configuration["OAuth:Microsoft:ClientId"] ?? "",
                ClientSecret = _configuration["OAuth:Microsoft:ClientSecret"] ?? "",
                Scope = "openid email profile User.Read"
            },
            _ => throw new ArgumentException($"Provider {provider} not supported")
        };
    }

    public async Task<OAuthTokenResponse?> ExchangeCodeForTokenAsync(
        ExternalAuthProvider provider,
        string code,
        string redirectUri,
        string codeVerifier)
    {
        var config = GetProviderConfig(provider);

        var parameters = new Dictionary<string, string>
        {
            { "grant_type", "authorization_code" },
            { "code", code },
            { "redirect_uri", redirectUri },
            { "client_id", config.ClientId },
            { "code_verifier", codeVerifier } // AJOUT du code_verifier
        };

        // Ajouter client_secret sauf pour Twitter qui utilise Basic Auth
        if (provider != ExternalAuthProvider.Twitter)
        {
            parameters.Add("client_secret", config.ClientSecret);
        }

        // Twitter nécessite une authentification Basic
        if (provider == ExternalAuthProvider.Twitter)
        {
            var authValue = Convert.ToBase64String(
                Encoding.UTF8.GetBytes($"{config.ClientId}:{config.ClientSecret}"));
            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Basic", authValue);
        }

        try
        {
            var content = new FormUrlEncodedContent(parameters);
            var response = await _httpClient.PostAsync(config.TokenEndpoint, content);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                _logger.LogError("Token exchange failed for {Provider}: {Error}", provider, error);
                return null;
            }

            var json = await response.Content.ReadAsStringAsync();
            var tokenData = JsonSerializer.Deserialize<JsonElement>(json);

            return new OAuthTokenResponse
            {
                AccessToken = tokenData.GetProperty("access_token").GetString() ?? "",
                RefreshToken = tokenData.TryGetProperty("refresh_token", out var rt) ? rt.GetString() : null,
                IdToken = tokenData.TryGetProperty("id_token", out var it) ? it.GetString() : null,
                ExpiresIn = tokenData.TryGetProperty("expires_in", out var exp) ? exp.GetInt32() : 3600,
                TokenType = tokenData.TryGetProperty("token_type", out var tt) ? tt.GetString() ?? "Bearer" : "Bearer"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exchanging code for token with {Provider}", provider);
            return null;
        }
        finally
        {
            // Nettoyer le header d'autorisation
            _httpClient.DefaultRequestHeaders.Authorization = null;
        }
    }

    public async Task<ExternalUserInfo?> GetUserInfoAsync(
        ExternalAuthProvider provider,
        string accessToken)
    {
        try
        {
            return provider switch
            {
                ExternalAuthProvider.Google => await GetGoogleUserInfo(accessToken),
                ExternalAuthProvider.Facebook => await GetFacebookUserInfo(accessToken),
                ExternalAuthProvider.Twitter => await GetTwitterUserInfo(accessToken),
                ExternalAuthProvider.Microsoft => await GetMicrosoftUserInfo(accessToken),
                _ => null
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting user info from {Provider}", provider);
            return null;
        }
    }

    private async Task<ExternalUserInfo?> GetGoogleUserInfo(string accessToken)
    {
        _httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await _httpClient.GetAsync("https://www.googleapis.com/oauth2/v2/userinfo");

        if (!response.IsSuccessStatusCode) return null;

        var json = await response.Content.ReadAsStringAsync();
        var data = JsonSerializer.Deserialize<JsonElement>(json);

        return new ExternalUserInfo
        {
            Id = data.GetProperty("id").GetString() ?? "",
            Email = data.GetProperty("email").GetString() ?? "",
            Name = data.TryGetProperty("name", out var name) ? name.GetString() : null,
            AvatarUrl = data.TryGetProperty("picture", out var picture) ? picture.GetString() : null
        };
    }

    private async Task<ExternalUserInfo?> GetFacebookUserInfo(string accessToken)
    {
        var response = await _httpClient.GetAsync(
            $"https://graph.facebook.com/me?fields=id,name,email,picture&access_token={accessToken}");

        if (!response.IsSuccessStatusCode) return null;

        var json = await response.Content.ReadAsStringAsync();
        var data = JsonSerializer.Deserialize<JsonElement>(json);

        return new ExternalUserInfo
        {
            Id = data.GetProperty("id").GetString() ?? "",
            Email = data.TryGetProperty("email", out var email) ? email.GetString() ?? "" : "",
            Name = data.TryGetProperty("name", out var name) ? name.GetString() : null,
            AvatarUrl = data.TryGetProperty("picture", out var pic)
                ? pic.GetProperty("data").GetProperty("url").GetString()
                : null
        };
    }

    private async Task<ExternalUserInfo?> GetTwitterUserInfo(string accessToken)
    {
        _httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await _httpClient.GetAsync(
            "https://api.twitter.com/2/users/me?user.fields=profile_image_url");

        if (!response.IsSuccessStatusCode) return null;

        var json = await response.Content.ReadAsStringAsync();
        var data = JsonSerializer.Deserialize<JsonElement>(json);
        var userData = data.GetProperty("data");

        return new ExternalUserInfo
        {
            Id = userData.GetProperty("id").GetString() ?? "",
            Email = userData.GetProperty("username").GetString() + "@twitter.placeholder",
            Name = userData.TryGetProperty("name", out var name) ? name.GetString() : null,
            AvatarUrl = userData.TryGetProperty("profile_image_url", out var pic) ? pic.GetString() : null
        };
    }

    private async Task<ExternalUserInfo?> GetMicrosoftUserInfo(string accessToken)
    {
        _httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await _httpClient.GetAsync("https://graph.microsoft.com/v1.0/me");

        if (!response.IsSuccessStatusCode) return null;

        var json = await response.Content.ReadAsStringAsync();
        var data = JsonSerializer.Deserialize<JsonElement>(json);

        return new ExternalUserInfo
        {
            Id = data.GetProperty("id").GetString() ?? "",
            Email = data.TryGetProperty("mail", out var mail)
                ? mail.GetString() ?? ""
                : data.GetProperty("userPrincipalName").GetString() ?? "",
            Name = data.TryGetProperty("displayName", out var displayName) ? displayName.GetString() : null,
            AvatarUrl = null
        };
    }
}

public class ExternalUserInfo
{
    public string Id { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Name { get; set; }
    public string? AvatarUrl { get; set; }
}
