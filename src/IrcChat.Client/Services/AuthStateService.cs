namespace IrcChat.Client.Services;

public class AuthStateService
{
    private string? _token;
    private string? _username;

    public event Action? OnAuthStateChanged;

    public bool IsAuthenticated => !string.IsNullOrEmpty(_token);
    public string? Username => _username;
    public string? Token => _token;

    public void SetAuthState(string token, string username)
    {
        _token = token;
        _username = username;
        OnAuthStateChanged?.Invoke();
    }

    public void ClearAuthState()
    {
        _token = null;
        _username = null;
        OnAuthStateChanged?.Invoke();
    }
}