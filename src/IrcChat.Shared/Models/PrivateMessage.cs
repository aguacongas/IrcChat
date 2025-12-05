namespace IrcChat.Shared.Models;

public class PrivateMessage
{
    public Guid Id { get; set; }

    public string SenderUsername { get; set; } = string.Empty;

    public string RecipientUsername { get; set; } = string.Empty;

    public string Content { get; set; } = string.Empty;

    public DateTime Timestamp { get; set; }

    public bool IsRead { get; set; }

    public string SenderUserId { get; set; } = string.Empty;

    public string RecipientUserId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether indique si le message a été supprimé par l'expéditeur.
    /// Si true, le message n'apparaîtra plus dans la vue de l'expéditeur.
    /// </summary>
    public bool IsDeletedBySender { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether indique si le message a été supprimé par le destinataire.
    /// Si true, le message n'apparaîtra plus dans la vue du destinataire.
    /// </summary>
    public bool IsDeletedByRecipient { get; set; }
}