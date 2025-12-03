using Microsoft.AspNetCore.Authorization;

namespace IrcChat.Api.Authorization;

public class UserIdMatchRequirement(string userId, string cookie) : IAuthorizationRequirement
{
    public string UserId { get; } = userId;
    public string Cookie { get; } = cookie;
}