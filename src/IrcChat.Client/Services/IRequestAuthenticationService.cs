namespace IrcChat.Client.Services;

public interface IRequestAuthenticationService
{
    string? ConnectionId { get; set; }

    string? Token { get; set; }
}