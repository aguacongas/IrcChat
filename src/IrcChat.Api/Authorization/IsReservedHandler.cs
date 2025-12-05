using System.Diagnostics.CodeAnalysis;
using System.Security.Claims;
using IrcChat.Api.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace IrcChat.Api.Authorization;

[SuppressMessage("Performance", "CA1862", Justification = "Not needed in SQL")]
public class IsReservedHandler(ChatDbContext db, ILogger<IsReservedHandler> logger)
    : AuthorizationHandler<IsReservedRequirement>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, IsReservedRequirement requirement)
    {
        var username = context.User.Claims
            .FirstOrDefault(c => c.Type == ClaimTypes.Name)?.Value;

        // Vérifier si l'utilisateur est Reserved
        var user = await db.ReservedUsernames
            .FirstOrDefaultAsync(u => username != null && u.Username.ToLower() == username.ToLower());

        if (user is null)
        {
            logger.LogWarning("Tentative d'accès à une api protégé pour un utilisateur reservé uniquement");
            context.Fail();
        }

        logger.LogInformation(
            "Utilisateur {Username} autorisé en tant qu'utilisateur reservé",
            username);
        context.Succeed(requirement);
    }
}