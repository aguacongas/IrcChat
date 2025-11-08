using System.Security.Claims;
using IrcChat.Api.Authorization;
using Microsoft.AspNetCore.Authorization;

namespace IrcChat.Api.Extensions;

public static class AuthorizationExtensions
{
    /// <summary>
    /// Ajoute les Authorization Handlers personnalisés
    /// </summary>
    public static IServiceCollection AddCustomAuthorization(this IServiceCollection services)
    => services.AddScoped<IAuthorizationHandler, ChannelModificationHandler>();

    /// <summary>
    /// Extension pour vérifier facilement l'autorisation de modification de canal
    /// </summary>
    public static async Task<bool> CanModifyChannelAsync(
        this IAuthorizationService authorizationService,
        ClaimsPrincipal user,
        string channelName)
    {
        var requirement = new ChannelModificationRequirement(channelName);
        var result = await authorizationService.AuthorizeAsync(user, null, requirement);
        return result.Succeeded;
    }
}