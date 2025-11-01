namespace IrcChat.Shared.Models;

public class SendPrivateMessageRequest
{
    public string SenderUsername { get; set; } = string.Empty;
    public string RecipientUsername { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
}