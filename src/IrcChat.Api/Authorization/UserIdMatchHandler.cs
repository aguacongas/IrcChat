using System.Security.Claims;
using IrcChat.Api.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace IrcChat.Api.Authorization;

public class UserIdMatchHandler(ChatDbContext db) : AuthorizationHandler<UserIdMatchRequirement>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, UserIdMatchRequirement requirement)
    {
        var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier) ??
            await GetUserIdFromCookieAsync(requirement);

        // si c'est un utilisateur identifié, on vérifie que l'id correspond
        if (userId == requirement.UserId)
        {
            context.Succeed(requirement);
            return;
        }

        context.Fail();
    }

    private Task<string?> GetUserIdFromCookieAsync(UserIdMatchRequirement requirement)
    => db.ConnectedUsers.Where(u => u.ConnectionId == requirement.ConnectionId)
            .Select(u => u.UserId)
            .FirstOrDefaultAsync();
}