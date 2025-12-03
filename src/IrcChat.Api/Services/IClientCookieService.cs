namespace IrcChat.Api.Services;

public interface IClientCookieService
{
    string CreateCookie(string clientUserId);

    string GetUserId(string cookie);
}
