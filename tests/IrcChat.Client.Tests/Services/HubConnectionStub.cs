using System.Net;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.SignalR.Protocol;
using Microsoft.Extensions.Logging;

namespace IrcChat.Client.Tests.Services;

public class HubConnectionStub : HubConnection
{
    public HubConnectionStub()
        : base(
            Mock.Of<IConnectionFactory>(),
            Mock.Of<IHubProtocol>(),
            Mock.Of<EndPoint>(),
            Mock.Of<IServiceProvider>(),
            GetLoggerFactory(),
            Mock.Of<IRetryPolicy>())
    {
    }

    private static ILoggerFactory GetLoggerFactory()
    {
        var loggerFactoryMock = new Mock<ILoggerFactory>();
        loggerFactoryMock.Setup(x => x.CreateLogger(It.IsAny<string>()))
            .Returns(Mock.Of<ILogger>());
        return loggerFactoryMock.Object;
    }
}