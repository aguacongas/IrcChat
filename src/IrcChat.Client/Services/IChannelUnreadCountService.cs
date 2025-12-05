// src/IrcChat.Client/Services/IChannelUnreadCountService.cs
namespace IrcChat.Client.Services;

/// <summary>
/// Service pour gérer les compteurs de messages non lus par salon.
/// </summary>
public interface IChannelUnreadCountService
{
    /// <summary>
    /// Événement déclenché quand les compteurs changent
    /// </summary>
    event Action? OnCountsChanged;

    /// <summary>
    /// Incrémente le compteur pour un salon.
    /// </summary>
    /// <param name="channel">Nom du salon.</param>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    Task IncrementCountAsync(string channel);

    /// <summary>
    /// Réinitialise le compteur pour un salon à 0.
    /// </summary>
    /// <param name="channel">Nom du salon.</param>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    Task ResetCountAsync(string channel);

    /// <summary>
    /// Obtient le compteur actuel pour un salon.
    /// </summary>
    /// <param name="channel">Nom du salon.</param>
    /// <returns>Nombre de messages non lus.</returns>
    int GetCount(string channel);

    /// <summary>
    /// Charge les compteurs depuis sessionStorage.
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    Task LoadFromStorageAsync();
}