using System.Security.Claims;
using IrcChat.Api.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace IrcChat.Api.Authorization;

public class UserIdMatchHandler(ChatDbContext db, IHttpContextAccessor httpContextAccessor) : AuthorizationHandler<UserIdMatchRequirement>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, UserIdMatchRequirement requirement)
    {
        var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier) ??
            await GetUserIdFromCookieAsync(requirement, httpContextAccessor.HttpContext?.RequestAborted ?? CancellationToken.None);

        // si c'est un utilisateur identifié, on vérifie que l'id correspond
        if (userId == requirement.UserId)
        {
            context.Succeed(requirement);
            return;
        }

        context.Fail();
    }

    private Task<string?> GetUserIdFromCookieAsync(UserIdMatchRequirement requirement, CancellationToken cancellationToken)
    => db.ConnectedUsers.Where(u => u.ConnectionId == requirement.ConnectionId)
            .Select(u => u.UserId)
            .FirstOrDefaultAsync(cancellationToken);
}