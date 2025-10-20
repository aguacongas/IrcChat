using System.Text.Json;

namespace IrcChat.Client.Services;

public class OAuthStateService(LocalStorageService localStorage)
{
    private const string AUTH_KEY = "ircchat_auth";

    private string? _token;
    private string? _username;
    private string? _email;
    private string? _avatarUrl;
    private Guid? _userId;
    private bool _isInitialized = false;

    public event Action? OnAuthStateChanged;

    public bool IsAuthenticated => !string.IsNullOrEmpty(_token);
    public string? Username => _username;
    public string? Token => _token;
    public string? Email => _email;
    public string? AvatarUrl => _avatarUrl;
    public Guid? UserId => _userId;

    public async Task InitializeAsync()
    {
        if (_isInitialized)
            return;

        await RestoreFromLocalStorageAsync();
        _isInitialized = true;
    }

    public async Task SetAuthStateAsync(string token, string username, string email, string? avatarUrl, Guid userId)
    {
        _token = token;
        _username = username;
        _email = email;
        _avatarUrl = avatarUrl;
        _userId = userId;

        await SaveToLocalStorageAsync();
        OnAuthStateChanged?.Invoke();
    }

    public async Task ClearAuthStateAsync()
    {
        _token = null;
        _username = null;
        _email = null;
        _avatarUrl = null;
        _userId = null;

        await ClearLocalStorageAsync();
        OnAuthStateChanged?.Invoke();
    }

    private async Task SaveToLocalStorageAsync()
    {
        var authData = new AuthData
        {
            Token = _token,
            Username = _username,
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
                var authData = JsonSerializer.Deserialize<AuthData>(json);
                if (authData != null)
                {
                    _token = authData.Token;
                    _username = authData.Username;
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

    private class AuthData
    {
        public string? Token { get; set; }
        public string? Username { get; set; }
        public string? Email { get; set; }
        public string? AvatarUrl { get; set; }
        public Guid? UserId { get; set; }
    }
}
