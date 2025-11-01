// src/IrcChat.Api/Services/AutoMuteOptions.cs
namespace IrcChat.Api.Services;

public class AutoMuteOptions
{
    public const string SectionName = "AutoMute";

    /// <summary>
    /// Nombre de minutes d'inactivité du propriétaire avant auto-mute du salon.
    /// Par défaut: 5 minutes.
    /// </summary>
    public int InactivityMinutes { get; set; } = 5;

    /// <summary>
    /// Intervalle de vérification en secondes.
    /// Par défaut: 30 secondes.
    /// </summary>
    public int CheckIntervalSeconds { get; set; } = 30;
}