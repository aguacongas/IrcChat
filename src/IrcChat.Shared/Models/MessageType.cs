namespace IrcChat.Shared.Models;

/// <summary>
/// Type de message.
/// </summary>
public enum MessageType
{
    /// <summary>
    /// Message texte uniquement.
    /// </summary>
    Text = 0,
    /// <summary>
    /// Image éphémère uniquement.
    /// </summary>
    Image = 1,

    /// <summary>
    /// Message texte avec image éphémère.
    /// </summary>
    TextWithImage = 2
}