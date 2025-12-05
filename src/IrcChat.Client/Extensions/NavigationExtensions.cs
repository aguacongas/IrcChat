using Microsoft.AspNetCore.Components;

namespace IrcChat.Client.Extensions;

/// <summary>
/// Extensions pour NavigationManager qui respectent le baseHref.
/// </summary>
public static class NavigationExtensions
{
    /// <summary>
    /// Navigue vers une URL relative en respectant le baseHref.
    /// </summary>
    /// <param name="navigationManager">Instance de NavigationManager.</param>
    /// <param name="uri">URI relative (ex: "chat", "/chat", "settings").</param>
    /// <param name="forceLoad">Force le rechargement de la page.</param>
    public static void NavigateToRelative(this NavigationManager navigationManager, string uri, bool forceLoad = false)
    {
        // Supprimer le slash initial si présent
        var cleanUri = uri.TrimStart('/');

        // Construire l'URL complète en utilisant ToAbsoluteUri
        var absoluteUri = navigationManager.ToAbsoluteUri(cleanUri);

        navigationManager.NavigateTo(absoluteUri.ToString(), forceLoad);
    }
}