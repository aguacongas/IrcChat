// src/IrcChat.Client/Services/UnifiedAuthService.cs
using System.Text.Json;
using IrcChat.Shared.Models;
using Microsoft.JSInterop;

namespace IrcChat.Client.Services;

public class UnifiedAuthService(ILocalStorageService localStorage,
    HttpClient httpClient,
    IJSRuntime jsRuntime,
    IRequestAuthenticationService requestAuthService,
    ILogger<UnifiedAuthService> logger) : IUnifiedAuthService
{
    private static readonly string AuthKey = "ircchat_unified_auth";
    private bool isInitialized = false;
    private IJSObjectReference? userIdModule;
    private string? clientUserId;

    public event Action? OnAuthStateChanged;

    public bool HasUsername => !string.IsNullOrEmpty(Username);

    public bool IsReserved { get; private set; }

    public bool IsAuthenticated => !string.IsNullOrEmpty(Token);

    public bool IsAdmin { get; private set; }

    public string? Username { get; private set; }

    public string? Token { get => requestAuthService.Token; private set => requestAuthService.Token = value; }

    public ExternalAuthProvider? ReservedProvider { get; private set; }

    public string? Email { get; private set; }

    public string? AvatarUrl { get; private set; }

    public Guid? UserId { get; private set; }

    public bool CanForgetUsername => HasUsername && (!IsReserved || IsAuthenticated);

    public bool IsNoPvMode { get; private set; }

    public async Task InitializeAsync()
    {
        if (isInitialized)
        {
            return;
        }

        await RestoreFromLocalStorageAsync();

        isInitialized = true;
    }

    public async Task SetUsernameAsync(string username, bool isReserved = false, ExternalAuthProvider? provider = null)
    {
        Username = username;
        IsReserved = isReserved;
        ReservedProvider = provider;

        await SaveToLocalStorageAsync();
        OnAuthStateChanged?.Invoke();
    }

    public async Task SetAuthStateAsync(string token, string username, string? email, string? avatarUrl, Guid userId, ExternalAuthProvider provider, bool isAdmin = false)
    {
        Token = token;
        Username = username;
        Email = email;
        AvatarUrl = avatarUrl;
        UserId = userId;
        IsReserved = true;
        ReservedProvider = provider;
        IsAdmin = isAdmin;

        await SaveToLocalStorageAsync();
        OnAuthStateChanged?.Invoke();
    }

    public async Task ForgetUsernameAndLogoutAsync()
    {
        if (IsAuthenticated && !string.IsNullOrEmpty(Token))
        {
            try
            {
                httpClient.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", Token);

                await httpClient.PostAsync("api/oauth/forget-username", null);

                httpClient.DefaultRequestHeaders.Authorization = null;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Erreur lors de la déconnexion côté serveur, ignorée");
            }
        }

        await ClearLocalStorageAsync();

        Username = null;
        Token = null;
        IsReserved = false;
        ReservedProvider = null;
        Email = null;
        AvatarUrl = null;
        UserId = null;
        IsAdmin = false;
        IsNoPvMode = false;

        OnAuthStateChanged?.Invoke();
    }

    public async Task LogoutAsync()
    {
        Token = null;
        Email = null;
        AvatarUrl = null;
        UserId = null;
        IsAdmin = false;

        await SaveToLocalStorageAsync();
        OnAuthStateChanged?.Invoke();
    }

    public async Task ClearAllAsync()
    {
        Username = null;
        Token = null;
        IsReserved = false;
        ReservedProvider = null;
        Email = null;
        AvatarUrl = null;
        UserId = null;
        IsAdmin = false;
        IsNoPvMode = false;

        await ClearLocalStorageAsync();
        OnAuthStateChanged?.Invoke();
    }

    public async Task<string> GetClientUserIdAsync()
    {
        if (!string.IsNullOrEmpty(clientUserId))
        {
            return clientUserId;
        }

        if (userIdModule == null)
        {
            try
            {
                userIdModule = await jsRuntime.InvokeAsync<IJSObjectReference>(
                    "import", "./js/userIdManager.js");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Erreur lors du chargement du module userIdManager");
                clientUserId = Guid.NewGuid().ToString();
                return clientUserId;
            }
        }

        if (IsReserved && !string.IsNullOrEmpty(Username) && UserId.HasValue)
        {
            return UserId.Value.ToString();
        }

        try
        {
            clientUserId = await userIdModule.InvokeAsync<string>("getUserId");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Erreur lors de la récupération du UserId depuis IndexedDB");
            clientUserId = Guid.NewGuid().ToString();
        }

        logger.LogInformation(
            "ClientUserId récupéré: {UserId} (IsReserved: {IsReserved})",
            clientUserId,
            IsReserved);

        return clientUserId;
    }

    public async Task SetNoPvModeAsync(bool enabled)
    {
        IsNoPvMode = enabled;
        await SaveToLocalStorageAsync();
        OnAuthStateChanged?.Invoke();
    }

    private async Task SaveToLocalStorageAsync()
    {
        var authData = new UnifiedAuthData
        {
            Username = Username,
            Token = Token,
            IsReserved = IsReserved,
            ReservedProvider = ReservedProvider,
            Email = Email,
            AvatarUrl = AvatarUrl,
            UserId = UserId,
            IsAdmin = IsAdmin,
            IsNoPvMode = IsNoPvMode,
        };

        var json = JsonSerializer.Serialize(authData);
        await localStorage.SetItemAsync(AuthKey, json);
    }

    private async Task RestoreFromLocalStorageAsync()
    {
        try
        {
            var json = await localStorage.GetItemAsync(AuthKey);
            if (!string.IsNullOrEmpty(json))
            {
                var authData = JsonSerializer.Deserialize<UnifiedAuthData>(json);
                if (authData != null)
                {
                    Username = authData.Username;
                    Token = authData.Token;
                    IsReserved = authData.IsReserved;
                    ReservedProvider = authData.ReservedProvider;
                    Email = authData.Email;
                    AvatarUrl = authData.AvatarUrl;
                    UserId = authData.UserId;
                    IsAdmin = authData.IsAdmin;
                    IsNoPvMode = authData.IsNoPvMode;
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Erreur lors de la lecture des données d'authentification.");
        }
    }

    private async Task ClearLocalStorageAsync() => await localStorage.RemoveItemAsync(AuthKey);

    private sealed class UnifiedAuthData
    {
        public string? Username { get; set; }

        public string? Token { get; set; }

        public bool IsReserved { get; set; }

        public ExternalAuthProvider? ReservedProvider { get; set; }

        public string? Email { get; set; }

        public string? AvatarUrl { get; set; }

        public Guid? UserId { get; set; }

        public bool IsAdmin { get; set; }

        public bool IsNoPvMode { get; set; }
    }
}