namespace IrcChat.Client.Services;

public interface IAuthStateService
{
    event Action? OnAuthStateChanged;

    bool IsAuthenticated { get; }
    string? Username { get; }
    string? Token { get; }

    void SetAuthState(string token, string username);
    void ClearAuthState();
}
