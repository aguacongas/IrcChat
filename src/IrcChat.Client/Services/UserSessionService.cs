using System.Text.Json;

namespace IrcChat.Client.Services;

public class UserSessionService(LocalStorageService localStorage)
{
    private const string SESSION_KEY = "ircchat_guest_session";

    private string? _username;
    private bool _isInitialized = false;

    public event Action? OnSessionChanged;

    public bool IsLoggedIn => !string.IsNullOrEmpty(_username);
    public string? Username => _username;

    public async Task InitializeAsync()
    {
        if (_isInitialized)
            return;

        await RestoreFromLocalStorageAsync();
        _isInitialized = true;
    }

    public async Task SetUsernameAsync(string username)
    {
        _username = username;
        await SaveToLocalStorageAsync();
        OnSessionChanged?.Invoke();
    }

    public void SetUsername(string username)
    {
        _username = username;
        _ = SaveToLocalStorageAsync();
        OnSessionChanged?.Invoke();
    }

    public async Task ClearSessionAsync()
    {
        _username = null;
        await ClearLocalStorageAsync();
        OnSessionChanged?.Invoke();
    }

    public void ClearSession()
    {
        _username = null;
        _ = ClearLocalStorageAsync();
        OnSessionChanged?.Invoke();
    }

    private async Task SaveToLocalStorageAsync()
    {
        var sessionData = new SessionData { Username = _username };
        var json = JsonSerializer.Serialize(sessionData);
        await localStorage.SetItemAsync(SESSION_KEY, json);
    }

    private async Task RestoreFromLocalStorageAsync()
    {
        try
        {
            var json = await localStorage.GetItemAsync(SESSION_KEY);
            if (!string.IsNullOrEmpty(json))
            {
                var sessionData = JsonSerializer.Deserialize<SessionData>(json);
                if (sessionData != null)
                {
                    _username = sessionData.Username;
                }
            }
        }
        catch { }
    }

    private async Task ClearLocalStorageAsync()
    {
        await localStorage.RemoveItemAsync(SESSION_KEY);
    }

    private class SessionData
    {
        public string? Username { get; set; }
    }
}
