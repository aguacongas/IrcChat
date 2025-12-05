// src/IrcChat.Client/Services/ChannelUnreadCountService.cs
using System.Diagnostics.CodeAnalysis;
using Microsoft.JSInterop;

namespace IrcChat.Client.Services;

public class ChannelUnreadCountService(IJSRuntime jsRuntime, ILogger<ChannelUnreadCountService> logger)
    : IChannelUnreadCountService, IAsyncDisposable
{
    private IJSObjectReference? module;
    private bool isInitialized;
    private Dictionary<string, int> counts = [];

    public event Action? OnCountsChanged;

    public async Task IncrementCountAsync(string channel)
    {
        if (string.IsNullOrWhiteSpace(channel))
        {
            return;
        }

        await EnsureInitializedAsync();

        if (counts.TryGetValue(channel, out var value))
        {
            counts[channel] = value + 1;
        }
        else
        {
            counts[channel] = 1;
        }

        await SaveToStorageAsync();
        OnCountsChanged?.Invoke();

        logger.LogDebug("Compteur incrémenté pour {Channel}: {Count}", channel, counts[channel]);
    }

    public async Task ResetCountAsync(string channel)
    {
        if (string.IsNullOrWhiteSpace(channel))
        {
            return;
        }

        await EnsureInitializedAsync();

        if (counts.ContainsKey(channel))
        {
            counts[channel] = 0;
            await SaveToStorageAsync();
            OnCountsChanged?.Invoke();

            logger.LogDebug("Compteur réinitialisé pour {Channel}", channel);
        }
    }

    public int GetCount(string channel)
    {
        if (string.IsNullOrWhiteSpace(channel))
        {
            return 0;
        }

        return counts.TryGetValue(channel, out var count) ? count : 0;
    }

    public async Task LoadFromStorageAsync()
    {
        await EnsureInitializedAsync();

        try
        {
            var countsDict = await module!.InvokeAsync<Dictionary<string, int>>("getUnreadCounts");
            if (countsDict != null && countsDict.Count > 0)
            {
                counts = countsDict;
                OnCountsChanged?.Invoke();

                logger.LogInformation("Compteurs chargés depuis sessionStorage: {Count} salons", counts.Count);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Erreur lors du chargement des compteurs depuis sessionStorage");
            counts = [];
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (module != null)
        {
            try
            {
                await module.DisposeAsync();
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Erreur lors de la libération du module de compteurs non lus");
            }
        }

        GC.SuppressFinalize(this);
    }

    private async Task SaveToStorageAsync()
    {
        if (module == null)
        {
            return;
        }

        try
        {
            await module.InvokeVoidAsync("saveUnreadCounts", counts);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Erreur lors de la sauvegarde des compteurs dans sessionStorage");
        }
    }

    [SuppressMessage("Major Code Smell", "S2139:Exceptions should be either logged or rethrown but not both", Justification = "It's logged")]
    private async Task EnsureInitializedAsync()
    {
        if (isInitialized)
        {
            return;
        }

        try
        {
            module = await jsRuntime.InvokeAsync<IJSObjectReference>(
                "import", "./js/channel-unread-count.js");
            isInitialized = true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Erreur lors du chargement du module de compteurs non lus");
            throw;
        }
    }
}