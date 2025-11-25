using Microsoft.AspNetCore.Authorization;

namespace IrcChat.Api.Authorization;

/// <summary>
/// Requirement pour vérifier qu'un utilisateur est admin.
/// </summary>
public class IsAdminRequirement : IAuthorizationRequirement
{
}