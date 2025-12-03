// src/IrcChat.Client/Services/IActiveChannelsService.cs

namespace IrcChat.Client.Services;

public interface IActiveChannelsService
{
    /// <summary>
    /// Initialise le service et charge les salons actifs depuis localStorage
    /// </summary>
    Task InitializeAsync();

    /// <summary>
    /// Ajoute un salon aux salons actifs
    /// </summary>
    Task AddChannelAsync(string channelName);

    /// <summary>
    /// Retire un salon des salons actifs
    /// </summary>
    Task RemoveChannelAsync(string channelName);

    /// <summary>
    /// Récupère la liste des salons actifs
    /// </summary>
    Task<List<string>> GetActiveChannelsAsync();

    /// <summary>
    /// Efface tous les salons actifs
    /// </summary>
    Task ClearAsync();
}