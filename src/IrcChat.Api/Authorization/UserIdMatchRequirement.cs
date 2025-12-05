using Microsoft.AspNetCore.Authorization;

namespace IrcChat.Api.Authorization;

public class UserIdMatchRequirement(string userId, string? connectionId) : IAuthorizationRequirement
{
    public string UserId { get; } = userId;

    public string? ConnectionId { get; } = connectionId;
}