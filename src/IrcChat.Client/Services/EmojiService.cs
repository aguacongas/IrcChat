using System.Diagnostics.CodeAnalysis;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using IrcChat.Client.Models;

namespace IrcChat.Client.Services;

/// <summary>
/// Service de gestion des emojis (chargement, recherche, conversion)
/// </summary>
public partial class EmojiService(HttpClient http, ILogger<EmojiService> logger) : IEmojiService
{
    private EmojiData? _emojiData;

    /// <summary>
    /// Charge les données emoji depuis emojis.json
    /// </summary>
    [SuppressMessage("Major Code Smell", "S2139:Exceptions should be either logged or rethrown but not both", Justification = "false positive")]
    public async Task InitializeAsync()
    {
        try
        {
            logger.LogInformation("Chargement des données emoji depuis emojis.json");

            _emojiData = await http.GetFromJsonAsync<EmojiData>("data/emojis.json");

            if (_emojiData == null || _emojiData.Emojis.Count == 0)
            {
                logger.LogError("Données emoji vides ou invalides");
                throw new InvalidOperationException("Les données emoji n'ont pas pu être chargées");
            }

            _codeToEmojiMap = BuildCodeMap(_emojiData);
            IsLoaded = true;

            logger.LogInformation(
                "Emojis chargés avec succès: {Count} emojis, {CategoryCount} catégories, version {Version}",
                _emojiData.Emojis.Count,
                _emojiData.Categories.Count,
                _emojiData.Version);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Erreur lors du chargement des données emoji");
            throw;
        }
    }

    /// <summary>
    /// Construit le dictionnaire code → emoji pour O(1) lookup
    /// </summary>
    private static Dictionary<string, string> BuildCodeMap(EmojiData emojiData)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var emoji in emojiData.Emojis)
        {
            // Ajouter tous les aliases
            foreach (var alias in emoji.Aliases.Where(alias => !map.ContainsKey(alias)))
            {
                map[alias] = emoji.Emoji;
            }
        }

        return map;
    }

    /// <summary>
    /// Recherche des emojis par code, nom ou keywords
    /// </summary>
    public List<EmojiItem> SearchEmojis(string query)
    {
        if (!IsLoaded || _emojiData == null || string.IsNullOrWhiteSpace(query))
        {
            return [];
        }

        query = query.ToLowerInvariant().Trim();

        return [.. _emojiData.Emojis
            .Where(e =>
                e.Code.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                e.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                e.NameEn.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                e.Keywords.Any(k => k.Contains(query, StringComparison.OrdinalIgnoreCase)) ||
                e.Aliases.Any(a => a.Contains(query, StringComparison.OrdinalIgnoreCase)))
            .Take(50)];
    }

    /// <summary>
    /// Obtient tous les emojis d'une catégorie
    /// </summary>
    public List<EmojiItem> GetEmojisByCategory(string categoryId)
    {
        if (!IsLoaded || _emojiData == null || string.IsNullOrWhiteSpace(categoryId))
        {
            return [];
        }

        return [.. _emojiData.Emojis
            .Where(e => e.Category.Replace(" ", "").Replace("&", "")
                .Equals(categoryId.Replace("-", "").Replace("and", ""), StringComparison.OrdinalIgnoreCase))];
    }

    /// <summary>
    /// Obtient toutes les catégories disponibles
    /// </summary>
    public List<EmojiCategory> GetCategories() => IsLoaded && _emojiData != null ? _emojiData.Categories : [];

    /// <summary>
    /// Obtient tous les emojis
    /// </summary>
    public List<EmojiItem> GetAllEmojis() => IsLoaded && _emojiData != null ? _emojiData.Emojis : [];

    /// <summary>
    /// Indique si le service est initialisé
    /// </summary>
    public bool IsLoaded { get; private set; }

    /// <summary>
    /// Pattern regex pour détecter les codes emoji :xxx:
    /// </summary>
    [GeneratedRegex(@":[\w_+-]+:", RegexOptions.Compiled, matchTimeoutMilliseconds: 1000)]
    private static partial Regex EmojiCodeRegex();
}