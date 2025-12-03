using Microsoft.AspNetCore.DataProtection;

namespace IrcChat.Api.Services;

public class ClientCookieService(IDataProtectionProvider dataProtectionProvider) : IClientCookieService
{
    private readonly IDataProtector _dataProtector = dataProtectionProvider.CreateProtector("ClientUserId");
    public string CreateCookie(string clientUserId)
    => _dataProtector.Protect(clientUserId);

    public string GetUserId(string cookie)
    => _dataProtector.Unprotect(cookie);
}