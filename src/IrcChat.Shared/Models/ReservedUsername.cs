namespace IrcChat.Shared.Models;

/// <summary>
/// Represents a reserved username linked to an external OAuth provider.
/// </summary>
public class ReservedUsername
{
    /// <summary>
    /// Gets or sets the unique identifier for the reserved username.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the username.
    /// </summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the external authentication provider.
    /// </summary>
    public ExternalAuthProvider Provider { get; set; }

    /// <summary>
    /// Gets or sets the external user ID from the OAuth provider.
    /// </summary>
    public string ExternalUserId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the user's email address.
    /// </summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the user's display name from the OAuth provider.
    /// </summary>
    public string? DisplayName { get; set; }

    /// <summary>
    /// Gets or sets the user's avatar URL from the OAuth provider.
    /// </summary>
    public string? AvatarUrl { get; set; }

    /// <summary>
    /// Gets or sets the date of birth for age verification (COPPA compliance).
    /// Stored in UTC.
    /// </summary>
    public DateTime DateOfBirth { get; set; }

    /// <summary>
    /// Gets or sets the date and time when the username was reserved.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets the date and time of the user's last login.
    /// </summary>
    public DateTime LastLoginAt { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the user has administrator privileges.
    /// </summary>
    public bool IsAdmin { get; set; } = false;
}