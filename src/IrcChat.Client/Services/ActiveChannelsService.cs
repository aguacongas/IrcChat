// src/IrcChat.Client/Services/ActiveChannelsService.cs

using System.Text.Json;
using Microsoft.JSInterop;

namespace IrcChat.Client.Services;

public sealed class ActiveChannelsService(IJSRuntime jsRuntime, ILogger<ActiveChannelsService> logger) : IActiveChannelsService
{
    private static readonly string StorageKey = "active-channels";
    private List<string> activeChannels = [];
    private bool isInitialized = false;

    public async Task InitializeAsync()
    {
        if (isInitialized)
        {
            return;
        }

        try
        {
            var json = await jsRuntime.InvokeAsync<string?>("localStorage.getItem", StorageKey);

            if (!string.IsNullOrEmpty(json))
            {
                var channels = JsonSerializer.Deserialize<List<string>>(json);
                if (channels != null)
                {
                    activeChannels = channels;
                    logger.LogInformation(
                        "Salons actifs chargés: {Count} salons - {Channels}",
                        activeChannels.Count,
                        string.Join(", ", activeChannels));
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Erreur lors du chargement des salons actifs");
            activeChannels = [];
        }

        isInitialized = true;
    }

    public async Task AddChannelAsync(string channelName)
    {
        if (!isInitialized)
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
            activeChannels.RemoveAll(c =>
                c.Equals(normalizedName, StringComparison.OrdinalIgnoreCase));

            activeChannels.Add(normalizedName);
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
        if (!isInitialized)
        {
            await InitializeAsync();
        }

        try
        {
            var removed = activeChannels.RemoveAll(c =>
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
        if (!isInitialized)
        {
            await InitializeAsync();
        }

        return [.. activeChannels];
    }

    public async Task ClearAsync()
    {
        try
        {
            activeChannels.Clear();
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
            var json = JsonSerializer.Serialize(activeChannels);
            await jsRuntime.InvokeVoidAsync("localStorage.setItem", StorageKey, json);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Erreur lors de la sauvegarde des salons actifs");
        }
    }
}