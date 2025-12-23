using System.ComponentModel.DataAnnotations;
using IrcChat.Shared.Validation;

namespace IrcChat.Shared.Models;

/// <summary>
/// Request to reserve a username with an OAuth provider.
/// </summary>
public class ReserveUsernameRequest
{
    /// <summary>
    /// Gets or sets the username to reserve.
    /// </summary>
    [Required(ErrorMessage = "Le pseudo est requis")]
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the OAuth provider.
    /// </summary>
    [Required(ErrorMessage = "Le provider est requis")]
    public ExternalAuthProvider Provider { get; set; }

    /// <summary>
    /// Gets or sets the authorization code from OAuth.
    /// </summary>
    [Required(ErrorMessage = "Le code d'autorisation est requis")]
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the redirect URI used in the OAuth flow.
    /// </summary>
    [Required(ErrorMessage = "L'URI de redirection est requise")]
    public string RedirectUri { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the PKCE code verifier.
    /// </summary>
    [Required(ErrorMessage = "Le code verifier est requis")]
    public string CodeVerifier { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the user ID (for guest users upgrading to reserved).
    /// </summary>
    [Required(ErrorMessage = "L'identifiant utilisateur est requis")]
    public Guid UserId { get; set; }

    /// <summary>
    /// Gets or sets the date of birth for age verification.
    /// Must be at least 13 years old (COPPA compliance).
    /// </summary>
    [Required(ErrorMessage = "La date de naissance est requise")]
    [MinimumAge(13, MaximumAge = 120)]
    public DateTime DateOfBirth { get; set; }
}