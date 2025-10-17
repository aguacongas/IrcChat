namespace IrcChat.Client.Services;

public class UserSessionService
{
    private string? _username;

    public event Action? OnSessionChanged;

    public bool IsLoggedIn => !string.IsNullOrEmpty(_username);
    public string? Username => _username;

    public void SetUsername(string username)
    {
        _username = username;
        OnSessionChanged?.Invoke();
    }

    public void ClearSession()
    {
        _username = null;
        OnSessionChanged?.Invoke();
    }
}