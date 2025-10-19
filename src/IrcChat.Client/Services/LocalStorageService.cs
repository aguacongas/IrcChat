using Microsoft.JSInterop;

namespace IrcChat.Client.Services;

public class LocalStorageService(IJSRuntime jsRuntime)
{
    public async Task SetItemAsync(string key, string value)
    {
        await jsRuntime.InvokeVoidAsync("localStorageHelper.setItem", key, value);
    }

    public async Task<string?> GetItemAsync(string key)
    {
        return await jsRuntime.InvokeAsync<string?>("localStorageHelper.getItem", key);
    }

    public async Task RemoveItemAsync(string key)
    {
        await jsRuntime.InvokeVoidAsync("localStorageHelper.removeItem", key);
    }

    public async Task ClearAsync()
    {
        await jsRuntime.InvokeVoidAsync("localStorageHelper.clear");
    }
}
