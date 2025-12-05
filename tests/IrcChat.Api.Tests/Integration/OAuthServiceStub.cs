using IrcChat.Api.Services;
using Microsoft.Extensions.Configuration;

namespace IrcChat.Api.Tests.Integration;

public class OAuthServiceStub : OAuthService
{
    public OAuthServiceStub()
        : base(null!, new ConfigurationManager().AddJsonFile("appsettings.json").Build(), null!)
    {
    }
}