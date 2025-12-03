using System.Security.Claims;
using IrcChat.Api.Services;
using Microsoft.AspNetCore.Authorization;

namespace IrcChat.Api.Authorization;

public class UserIdMatchHandler(IClientCookieService clientCookieService) : AuthorizationHandler<UserIdMatchRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, UserIdMatchRequirement requirement)
    {
        var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        // si c'est un utilisateur identifié, on vérifie que l'id correspond
        if (userId == requirement.UserId)
        {
            context.Succeed(requirement);
            return Task.CompletedTask;
        }

        if (userId == null && requirement.UserId == clientCookieService.GetUserId(requirement.Cookie))
        {
            // l'utilisateur n'est pas identifié, mais le cookie correspond
            context.Succeed(requirement);
            return Task.CompletedTask;
        }

        context.Fail();
        return Task.CompletedTask;
    }
}
