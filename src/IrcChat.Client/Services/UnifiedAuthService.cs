// src/IrcChat.Client/Services/UnifiedAuthService.cs
using IrcChat.Shared.Models;
using System.Text.Json;

namespace IrcChat.Client.Services;

public class UnifiedAuthService(LocalStorageService localStorage, HttpClient httpClient)
{
    private const string AUTH_KEY = "ircchat_unified_auth";

    private string? _username;
    private string? _token;
    private bool _isReserved;
    private ExternalAuthProvider? _reservedProvider;
    private string? _email;
    private string? _avatarUrl;
    private Guid? _userId;
    private bool _isAdmin;
    private bool _isInitialized = false;

    public event Action? OnAuthStateChanged;

    public bool HasUsername => !string.IsNullOrEmpty(_username);
    public bool IsReserved => _isReserved;
    public bool IsAuthenticated => !string.IsNullOrEmpty(_token);
    public bool IsAdmin => _isAdmin;
    public string? Username => _username;
    public string? Token => _token;
    public ExternalAuthProvider? ReservedProvider => _reservedProvider;
    public string? Email => _email;
    public string? AvatarUrl => _avatarUrl;
    public Guid? UserId => _userId;

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

    public async Task SetAuthStateAsync(string token, string username, string? email, string? avatarUrl, Guid userId, ExternalAuthProvider provider, bool isAdmin = false)
    {
        _token = token;
        _username = username;
        _email = email;
        _avatarUrl = avatarUrl;
        _userId = userId;
        _isReserved = true;
        _reservedProvider = provider;
        _isAdmin = isAdmin;

        await SaveToLocalStorageAsync();
        OnAuthStateChanged?.Invoke();
    }

    public async Task ForgetUsernameAndLogoutAsync()
    {
        if (IsAuthenticated && !string.IsNullOrEmpty(Token))
        {
            try
            {
                httpClient.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", Token);

                await httpClient.PostAsync("api/oauth/forget-username", null);

                httpClient.DefaultRequestHeaders.Authorization = null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erreur lors de l'appel API de déconnexion : {ex.Message}");
            }
        }

        await ClearLocalStorageAsync();

        _username = null;
        _token = null;
        _isReserved = false;
        _reservedProvider = null;
        _email = null;
        _avatarUrl = null;
        _userId = null;
        _isAdmin = false;

        OnAuthStateChanged?.Invoke();
    }

    public async Task LogoutAsync()
    {
        _token = null;
        _email = null;
        _avatarUrl = null;
        _userId = null;
        _isAdmin = false;

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
        _isAdmin = false;

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
            UserId = _userId,
            IsAdmin = _isAdmin
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
                    _isAdmin = authData.IsAdmin;
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
        public bool IsAdmin { get; set; }
    }
}