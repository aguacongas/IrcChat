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

    /// <summary>
    /// Gets a value indicating whether l'utilisateur est en mode "non MP".
    /// </summary>
    bool IsNoPvMode { get; }

    Task InitializeAsync();

    Task SetUsernameAsync(string username, bool isReserved = false, ExternalAuthProvider? provider = null);

    Task SetAuthStateAsync(string token, string username, string? email, string? avatarUrl, Guid userId, ExternalAuthProvider provider, bool isAdmin = false);

    Task ForgetUsernameAndLogoutAsync();

    Task LogoutAsync();

    Task ClearAllAsync();

    /// <summary>
    /// Obtient le UserId client (GUID pour invités, Username pour OAuth).
    /// </summary>
    Task<string> GetClientUserIdAsync();

    /// <summary>
    /// Active ou désactive le mode "non MP".
    /// </summary>
    Task SetNoPvModeAsync(bool enabled);
}