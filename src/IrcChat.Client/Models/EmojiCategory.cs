namespace IrcChat.Client.Models;

/// <summary>
/// Représente une catégorie d'emojis
/// </summary>
public class EmojiCategory
{
    /// <summary>
    /// Identifiant unique de la catégorie
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Nom affiché de la catégorie
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Emoji représentatif de la catégorie
    /// </summary>
    public string Icon { get; set; } = string.Empty;

    /// <summary>
    /// Nombre d'emojis dans cette catégorie
    /// </summary>
    public int Count { get; set; }

    /// <summary>
    /// Ordre d'affichage
    /// </summary>
    public int Order { get; set; }
}