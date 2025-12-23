using IrcChat.Shared.Models;

namespace IrcChat.Client.Services;

/// <summary>
/// Service for unified authentication state management (guests and OAuth users).
/// </summary>
public interface IUnifiedAuthService
{
    /// <summary>
    /// Event triggered when the authentication state changes.
    /// </summary>
    event Action? OnAuthStateChanged;

    /// <summary>
    /// Gets a value indicating whether the user has a username set.
    /// </summary>
    bool HasUsername { get; }

    /// <summary>
    /// Gets a value indicating whether the username is reserved (OAuth).
    /// </summary>
    bool IsReserved { get; }

    /// <summary>
    /// Gets a value indicating whether the user is authenticated (has a JWT token).
    /// </summary>
    bool IsAuthenticated { get; }

    /// <summary>
    /// Gets a value indicating whether the user is an administrator.
    /// </summary>
    bool IsAdmin { get; }

    /// <summary>
    /// Gets the current username.
    /// </summary>
    string? Username { get; }

    /// <summary>
    /// Gets the JWT token for authenticated users.
    /// </summary>
    string? Token { get; }

    /// <summary>
    /// Gets the OAuth provider used to reserve the username.
    /// </summary>
    ExternalAuthProvider? ReservedProvider { get; }

    /// <summary>
    /// Gets the user's email address (OAuth users only).
    /// </summary>
    string? Email { get; }

    /// <summary>
    /// Gets the user's avatar URL (OAuth users only).
    /// </summary>
    string? AvatarUrl { get; }

    /// <summary>
    /// Gets the user's unique identifier.
    /// </summary>
    Guid? UserId { get; }

    /// <summary>
    /// Gets a value indicating whether the user can forget their username.
    /// </summary>
    bool CanForgetUsername { get; }

    /// <summary>
    /// Gets a value indicating whether the user is in "no private message" mode.
    /// </summary>
    bool IsNoPvMode { get; }

    /// <summary>
    /// Gets the user's date of birth for age verification.
    /// </summary>
    DateTime? DateOfBirth { get; }

    /// <summary>
    /// Initializes the service by restoring the authentication state from local storage.
    /// </summary>
    Task InitializeAsync();

    /// <summary>
    /// Sets the username for a guest or reserved user.
    /// </summary>
    /// <param name="username">The username to set.</param>
    /// <param name="isReserved">Whether the username is reserved.</param>
    /// <param name="provider">The OAuth provider (if reserved).</param>
    Task SetUsernameAsync(string username, bool isReserved = false, ExternalAuthProvider? provider = null);

    /// <summary>
    /// Sets the full authentication state for OAuth users.
    /// </summary>
    /// <param name="token">The JWT token.</param>
    /// <param name="username">The username.</param>
    /// <param name="email">The email address.</param>
    /// <param name="avatarUrl">The avatar URL.</param>
    /// <param name="userId">The user ID.</param>
    /// <param name="provider">The OAuth provider.</param>
    /// <param name="isAdmin">Whether the user is an admin.</param>
    Task SetAuthStateAsync(string token, string username, string? email, string? avatarUrl, Guid userId, ExternalAuthProvider provider, bool isAdmin = false);

    /// <summary>
    /// Forgets the username and logs out the user completely.
    /// </summary>
    Task ForgetUsernameAndLogoutAsync();

    /// <summary>
    /// Logs out the user but keeps the username in memory.
    /// </summary>
    Task LogoutAsync();

    /// <summary>
    /// Clears all authentication state.
    /// </summary>
    Task ClearAllAsync();

    /// <summary>
    /// Gets the client user ID (GUID for guests, Username for OAuth).
    /// </summary>
    Task<string> GetClientUserIdAsync();

    /// <summary>
    /// Sets the "no private message" mode.
    /// </summary>
    /// <param name="enabled">Whether to enable the mode.</param>
    Task SetNoPvModeAsync(bool enabled);

    /// <summary>
    /// Sets the user's date of birth for age verification.
    /// </summary>
    /// <param name="dateOfBirth">The date of birth.</param>
    Task SetDateOfBirthAsync(DateTime dateOfBirth);
}