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
    private string? clientUserId; // UserId généré côté client

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
                // Ignore les erreurs de déconnexion côté serveur - l'utilisateur sera déconnecté localement de toute façon
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

        await ClearLocalStorageAsync();
        OnAuthStateChanged?.Invoke();
    }

    /// <summary>
    /// Obtient le UserId client (GUID pour invités, Username pour OAuth).
    /// </summary>
    /// <returns>Le UserId.</returns>
    public async Task<string> GetClientUserIdAsync()
    {
        if (!string.IsNullOrEmpty(clientUserId))
        {
            return clientUserId;
        }

        // Initialiser le module si nécessaire
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

                // Fallback : générer un GUID temporaire
                clientUserId = Guid.NewGuid().ToString();
                return clientUserId;
            }
        }

        if (IsReserved && !string.IsNullOrEmpty(Username) && UserId.HasValue)
        {
            // Utilisateur OAuth : clientUserId = Username
            return UserId.Value.ToString();
        }

        // Utilisateur invité : clientUserId = GUID depuis IndexedDB
        try
        {
            clientUserId = await userIdModule.InvokeAsync<string>("getUserId");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Erreur lors de la récupération du UserId depuis IndexedDB");

            // Fallback : générer un GUID
            clientUserId = Guid.NewGuid().ToString();
        }

        logger.LogInformation(
            "ClientUserId récupéré: {UserId} (IsReserved: {IsReserved})",
            clientUserId,
            IsReserved);

        return clientUserId;
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
    }
}