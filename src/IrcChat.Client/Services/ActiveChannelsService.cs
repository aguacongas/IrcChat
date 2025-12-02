// src/IrcChat.Client/Services/ActiveChannelsService.cs

using System.Text.Json;
using Microsoft.JSInterop;

namespace IrcChat.Client.Services;

public sealed class ActiveChannelsService(IJSRuntime jsRuntime, ILogger<ActiveChannelsService> logger) : IActiveChannelsService
{
    private static readonly string _storageKey = "active-channels";
    private List<string> _activeChannels = [];
    private bool _isInitialized = false;

    public async Task InitializeAsync()
    {
        if (_isInitialized)
        {
            return;
        }

        try
        {
            var json = await jsRuntime.InvokeAsync<string?>("localStorage.getItem", _storageKey);

            if (!string.IsNullOrEmpty(json))
            {
                var channels = JsonSerializer.Deserialize<List<string>>(json);
                if (channels != null)
                {
                    _activeChannels = channels;
                    logger.LogInformation("Salons actifs chargés: {Count} salons - {Channels}",
                        _activeChannels.Count, string.Join(", ", _activeChannels));
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Erreur lors du chargement des salons actifs");
            _activeChannels = [];
        }

        _isInitialized = true;
    }

    public async Task AddChannelAsync(string channelName)
    {
        if (!_isInitialized)
        {
            await InitializeAsync();
        }

        if (string.IsNullOrWhiteSpace(channelName))
        {
            return;
        }

        try
        {
            // on supprime et ajoute le salon à la fin pour savoir quel est le dernier salon rejoint
            var normalizedName = channelName.Trim();
            _activeChannels.RemoveAll(c =>
                c.Equals(normalizedName, StringComparison.OrdinalIgnoreCase));

            _activeChannels.Add(normalizedName);
            await SaveAsync();
            logger.LogDebug("Salon {ChannelName} ajouté aux salons actifs", normalizedName);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Erreur lors de l'ajout du salon {ChannelName} aux salons actifs", channelName);
        }
    }

    public async Task RemoveChannelAsync(string channelName)
    {
        if (!_isInitialized)
        {
            await InitializeAsync();
        }

        try
        {
            var removed = _activeChannels.RemoveAll(c =>
                c.Equals(channelName, StringComparison.OrdinalIgnoreCase));

            if (removed > 0)
            {
                await SaveAsync();
                logger.LogDebug("Salon {ChannelName} retiré des salons actifs", channelName);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Erreur lors de la suppression du salon {ChannelName} des salons actifs", channelName);
        }
    }

    public async Task<List<string>> GetActiveChannelsAsync()
    {
        if (!_isInitialized)
        {
            await InitializeAsync();
        }

        return [.. _activeChannels];
    }

    public async Task ClearAsync()
    {
        try
        {
            _activeChannels.Clear();
            await SaveAsync();
            logger.LogInformation("Salons actifs effacés");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Erreur lors de l'effacement des salons actifs");
        }
    }

    private async Task SaveAsync()
    {
        try
        {
            var json = JsonSerializer.Serialize(_activeChannels);
            await jsRuntime.InvokeVoidAsync("localStorage.setItem", _storageKey, json);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Erreur lors de la sauvegarde des salons actifs");
        }
    }
}