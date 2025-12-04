namespace IrcChat.Client.Services;

public class RequestAuthenticationService : IRequestAuthenticationService
{
    public string? ConnectionId { get; set; }

    public string? Token { get; set; }
}