// src/IrcChat.Client/Services/IActiveChannelsService.cs

namespace IrcChat.Client.Services;

public interface IActiveChannelsService
{
    /// <summary>
    /// Initialise le service et charge les salons actifs depuis localStorage.
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    Task InitializeAsync();

    /// <summary>
    /// Ajoute un salon aux salons actifs.
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    Task AddChannelAsync(string channelName);

    /// <summary>
    /// Retire un salon des salons actifs.
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    Task RemoveChannelAsync(string channelName);

    /// <summary>
    /// Récupère la liste des salons actifs.
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    Task<List<string>> GetActiveChannelsAsync();

    /// <summary>
    /// Efface tous les salons actifs.
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    Task ClearAsync();
}