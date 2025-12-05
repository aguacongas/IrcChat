namespace IrcChat.Shared.Models;

public class MuteStatusResponse
{
    public string UserId { get; set; } = string.Empty;

    public bool IsGloballyMuted { get; set; }
}