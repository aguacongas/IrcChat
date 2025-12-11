namespace IrcChat.Client.Models;

/// <summary>
/// Représente un emoji individuel avec ses métadonnées
/// </summary>
public class EmojiItem
{
    /// <summary>
    /// Le caractère emoji Unicode (ex: 😀)
    /// </summary>
    public string Emoji { get; set; } = string.Empty;

    /// <summary>
    /// Code GitHub style (ex: :smile:)
    /// </summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// Nom en français
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Nom en anglais
    /// </summary>
    public string NameEn { get; set; } = string.Empty;

    /// <summary>
    /// Catégorie principale (ex: Smileys & Emotion)
    /// </summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>
    /// Sous-catégorie Unicode
    /// </summary>
    public string Subcategory { get; set; } = string.Empty;

    /// <summary>
    /// Mots-clés de recherche (français + anglais)
    /// </summary>
    public List<string> Keywords { get; set; } = [];

    /// <summary>
    /// Aliases (codes alternatifs + styles IRC)
    /// </summary>
    public List<string> Aliases { get; set; } = [];

    /// <summary>
    /// Code point Unicode (ex: U+1F600)
    /// </summary>
    public string Unicode { get; set; } = string.Empty;

    /// <summary>
    /// Version Unicode d'introduction
    /// </summary>
    public string Version { get; set; } = string.Empty;
}