using Microsoft.AspNetCore.SignalR.Client;

namespace IrcChat.Client.Services;

public class HubConnectionEventWrapper(HubConnection connection) : IHubConnectionEvents
{
    public event Func<Exception, Task> Closed
    {
        add => connection.Closed += value;
        remove => connection.Closed -= value;
    }

    public event Func<Exception, Task> Reconnecting
    {
        add => connection.Reconnecting += value;
        remove => connection.Reconnecting -= value;
    }

    public event Func<string, Task> Reconnected
    {
        add => connection.Reconnected += value;
        remove => connection.Reconnected -= value;
    }
}