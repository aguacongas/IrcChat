namespace IrcChat.Client.Models;

/// <summary>
/// Représente les données complètes des emojis chargées depuis emojis.json
/// </summary>
public class EmojiData
{
    /// <summary>
    /// Version Unicode des emojis
    /// </summary>
    public string Version { get; set; } = string.Empty;

    /// <summary>
    /// Date de génération des données
    /// </summary>
    public DateTime GeneratedAt { get; set; }

    /// <summary>
    /// Liste complète des emojis
    /// </summary>
    public List<EmojiItem> Emojis { get; set; } = [];

    /// <summary>
    /// Liste des catégories d'emojis
    /// </summary>
    public List<EmojiCategory> Categories { get; set; } = [];
}