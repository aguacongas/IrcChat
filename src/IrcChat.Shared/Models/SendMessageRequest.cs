namespace IrcChat.Shared.Models;

public class SendMessageRequest
{
    public string Username { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string Channel { get; set; } = string.Empty;
}