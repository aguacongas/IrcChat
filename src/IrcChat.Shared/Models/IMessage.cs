
namespace IrcChat.Shared.Models;

public interface IMessage
{
    Guid Id { get; set; }
    DateTime Timestamp { get; set; }
}