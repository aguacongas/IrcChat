namespace IrcChat.Shared.Models;

public record SetClientCookieRequest
{
    public required string ClientUserId { get; init; }
}