using System.Diagnostics.CodeAnalysis;
using System.Security.Claims;
using IrcChat.Api.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace IrcChat.Api.Authorization;

[SuppressMessage("Performance", "CA1862", Justification = "Not needed in SQL")]
public class IsAdminHandler(ChatDbContext db, ILogger<ChannelModificationHandler> logger)
    : AuthorizationHandler<IsAdminRequirement>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, IsAdminRequirement requirement)
    {
        var username = context.User.Claims
            .FirstOrDefault(c => c.Type == ClaimTypes.Name)?.Value;

        // Vérifier si l'utilisateur est admin
        var user = await db.ReservedUsernames
            .FirstOrDefaultAsync(u => username != null && u.Username.ToLower() == username.ToLower());

        if (user is null || !user.IsAdmin)
        {
            logger.LogWarning("Tentative d'accès à une api protégé pour un administrateur uniquement");
            context.Fail();
        }

        logger.LogInformation("Utilisateur {Username} autorisé en tant qu'admin",
            username);
        context.Succeed(requirement);
    }
}
