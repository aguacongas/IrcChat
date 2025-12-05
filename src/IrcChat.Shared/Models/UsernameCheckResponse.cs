namespace IrcChat.Shared.Models;

public class UsernameCheckResponse
{
    public bool Available { get; set; }

    public bool IsReserved { get; set; }

    public ExternalAuthProvider? ReservedProvider { get; set; }

    public bool IsCurrentlyUsed { get; set; }
}