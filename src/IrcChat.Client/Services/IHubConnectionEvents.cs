namespace IrcChat.Client.Services;

public interface IHubConnectionEvents
{
    event Func<Exception, Task> Closed;
    event Func<Exception, Task> Reconnecting;
    event Func<string, Task> Reconnected;
}