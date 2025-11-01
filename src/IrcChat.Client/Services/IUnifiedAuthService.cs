using IrcChat.Shared.Models;

namespace IrcChat.Client.Services;

public interface IUnifiedAuthService
{
    event Action? OnAuthStateChanged;

    bool HasUsername { get; }
    bool IsReserved { get; }
    bool IsAuthenticated { get; }
    bool IsAdmin { get; }
    string? Username { get; }
    string? Token { get; }
    ExternalAuthProvider? ReservedProvider { get; }
    string? Email { get; }
    string? AvatarUrl { get; }
    Guid? UserId { get; }
    bool CanForgetUsername { get; }

    Task InitializeAsync();
    Task SetUsernameAsync(string username, bool isReserved = false, ExternalAuthProvider? provider = null);
    Task SetAuthStateAsync(string token, string username, string? email, string? avatarUrl, Guid userId, ExternalAuthProvider provider, bool isAdmin = false);
    Task ForgetUsernameAndLogoutAsync();
    Task LogoutAsync();
    Task ClearAllAsync();
}