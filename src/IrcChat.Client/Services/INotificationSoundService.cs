namespace IrcChat.Client.Services;

/// <summary>
/// Service pour gérer les notifications sonores de l'application.
/// </summary>
public interface INotificationSoundService
{
    /// <summary>
    /// Joue le son de notification si les sons sont activés.
    /// </summary>
    Task PlaySoundAsync();

    /// <summary>
    /// Active ou désactive les sons de notification.
    /// </summary>
    Task ToggleSoundAsync();

    /// <summary>
    /// Vérifie si les sons de notification sont activés.
    /// </summary>
    Task<bool> IsSoundEnabledAsync();
}