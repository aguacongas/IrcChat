namespace IrcChat.Client.Services;

public class AuthStateService : IAuthStateService
{
    public event Action? OnAuthStateChanged;

    public bool IsAuthenticated => !string.IsNullOrEmpty(Token);
    public string? Username { get; private set; }
    public string? Token { get; private set; }

    public void SetAuthState(string token, string username)
    {
        Token = token;
        Username = username;
        OnAuthStateChanged?.Invoke();
    }

    public void ClearAuthState()
    {
        Token = null;
        Username = null;
        OnAuthStateChanged?.Invoke();
    }
}