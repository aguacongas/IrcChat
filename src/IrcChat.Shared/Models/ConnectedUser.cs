namespace IrcChat.Shared.Models;

public class ConnectedUser
{
    public Guid Id { get; set; }

    public string Username { get; set; } = string.Empty;

    public string ConnectionId { get; set; } = string.Empty;

    public string? Channel { get; set; }

    public DateTime ConnectedAt { get; set; }

    public DateTime LastActivity { get; set; }

    public string ServerInstanceId { get; set; } = string.Empty;

    public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether l'utilisateur est en mode "non MP".
    /// Si true, l'utilisateur ne peut pas recevoir de messages privés non sollicités.
    /// </summary>
    public bool IsNoPvMode { get; set; }
}