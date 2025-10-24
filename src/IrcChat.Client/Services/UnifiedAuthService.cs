using IrcChat.Shared.Models;
using System.Text.Json;

namespace IrcChat.Client.Services;

public class UnifiedAuthService(LocalStorageService localStorage,
    HttpClient httpClient)
{
    private const string AUTH_KEY = "ircchat_unified_auth";

    private string? _username;
    private string? _token;
    private bool _isReserved;
    private ExternalAuthProvider? _reservedProvider;
    private string? _email;
    private string? _avatarUrl;
    private Guid? _userId;
    private bool _isInitialized = false;

    public event Action? OnAuthStateChanged;

    public bool HasUsername => !string.IsNullOrEmpty(_username);
    public bool IsReserved => _isReserved;
    public bool IsAuthenticated => !string.IsNullOrEmpty(_token);
    public string? Username => _username;
    public string? Token => _token;
    public ExternalAuthProvider? ReservedProvider => _reservedProvider;
    public string? Email => _email;
    public string? AvatarUrl => _avatarUrl;
    public Guid? UserId => _userId;

    // NOUVEAU: Indique si on peut oublier le pseudo
    public bool CanForgetUsername => HasUsername && (!IsReserved || IsAuthenticated);

    public async Task InitializeAsync()
    {
        if (_isInitialized)
            return;

        await RestoreFromLocalStorageAsync();
        _isInitialized = true;
    }

    public async Task SetUsernameAsync(string username, bool isReserved = false, ExternalAuthProvider? provider = null)
    {
        _username = username;
        _isReserved = isReserved;
        _reservedProvider = provider;

        await SaveToLocalStorageAsync();
        OnAuthStateChanged?.Invoke();
    }

    public async Task SetAuthStateAsync(string token, string username, string? email, string? avatarUrl, Guid userId, ExternalAuthProvider provider)
    {
        _token = token;
        _username = username;
        _email = email;
        _avatarUrl = avatarUrl;
        _userId = userId;
        _isReserved = true;
        _reservedProvider = provider;

        await SaveToLocalStorageAsync();
        OnAuthStateChanged?.Invoke();
    }

    public async Task ForgetUsernameAndLogoutAsync()
    {
        if (IsAuthenticated && !string.IsNullOrEmpty(Token))
        {
            try
            {
                // Appel API pour la déconnexion serveur (efface BDD et cookie d'auth)
                // L'appel doit utiliser le Token JWT comme Bearer.
                httpClient.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", Token);

                await httpClient.PostAsync("api/oauth/forget-username", null);

                // Retirer le header après l'appel
                httpClient.DefaultRequestHeaders.Authorization = null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erreur lors de l'appel API de déconnexion : {ex.Message}");
            }
        }

        // Nettoyage local (efface tous les cookies et le stockage)
        await ClearLocalStorageAsync();

        // Réinitialisation de l'état du service
        _username = null;
        _token = null;
        _isReserved = false;
        _reservedProvider = null;
        _email = null;
        _avatarUrl = null;
        _userId = null;

        OnAuthStateChanged?.Invoke();
    }

    public async Task LogoutAsync()
    {
        _token = null;
        _email = null;
        _avatarUrl = null;
        _userId = null;
        // NE PAS changer isReserved et reservedProvider
        // Le pseudo reste réservé mais on n'est plus authentifié

        await SaveToLocalStorageAsync();
        OnAuthStateChanged?.Invoke();
    }

    public async Task ClearAllAsync()
    {
        _username = null;
        _token = null;
        _isReserved = false;
        _reservedProvider = null;
        _email = null;
        _avatarUrl = null;
        _userId = null;

        await ClearLocalStorageAsync();
        OnAuthStateChanged?.Invoke();
    }

    private async Task SaveToLocalStorageAsync()
    {
        var authData = new UnifiedAuthData
        {
            Username = _username,
            Token = _token,
            IsReserved = _isReserved,
            ReservedProvider = _reservedProvider,
            Email = _email,
            AvatarUrl = _avatarUrl,
            UserId = _userId
        };

        var json = JsonSerializer.Serialize(authData);
        await localStorage.SetItemAsync(AUTH_KEY, json);
    }

    private async Task RestoreFromLocalStorageAsync()
    {
        try
        {
            var json = await localStorage.GetItemAsync(AUTH_KEY);
            if (!string.IsNullOrEmpty(json))
            {
                var authData = JsonSerializer.Deserialize<UnifiedAuthData>(json);
                if (authData != null)
                {
                    _username = authData.Username;
                    _token = authData.Token;
                    _isReserved = authData.IsReserved;
                    _reservedProvider = authData.ReservedProvider;
                    _email = authData.Email;
                    _avatarUrl = authData.AvatarUrl;
                    _userId = authData.UserId;
                }
            }
        }
        catch { }
    }

    private async Task ClearLocalStorageAsync()
    {
        await localStorage.RemoveItemAsync(AUTH_KEY);
    }

    private class UnifiedAuthData
    {
        public string? Username { get; set; }
        public string? Token { get; set; }
        public bool IsReserved { get; set; }
        public ExternalAuthProvider? ReservedProvider { get; set; }
        public string? Email { get; set; }
        public string? AvatarUrl { get; set; }
        public Guid? UserId { get; set; }
    }
}