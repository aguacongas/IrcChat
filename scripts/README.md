# Génération des données Emoji

## Vue d'ensemble

Les données emoji d'IrcChat sont générées automatiquement depuis **Unicode CLDR** (Common Locale Data Repository), la source officielle du Unicode Consortium pour les annotations emoji.

## Architecture

### Fichiers concernés

```
scripts/
  ├── generate-emoji-data.js    # Script de génération
  ├── package.json               # Configuration npm
  └── README.md                  # Ce fichier

src/IrcChat.Client/
  ├── wwwroot/data/
  │   └── emojis.json            # Données générées (300-500KB)
  ├── Models/
  │   └── EmojiData.cs           # Modèles C#
  └── Services/
      └── EmojiService.cs        # Service de gestion

tests/IrcChat.Client.Tests/
  ├── Services/
  │   └── EmojiServiceTests.cs
  └── Components/
      ├── EmojiPickerTests.cs
      ├── MessageInputTests.Emoji.cs
      └── MessageListTests.Emoji.cs
```

### Format JSON généré

```json
{
  "version": "15.1",
  "generatedAt": "2024-01-15T10:00:00Z",
  "emojis": [
    {
      "emoji": "😀",
      "code": ":grinning:",
      "name": "visage souriant",
      "nameEn": "grinning face",
      "category": "Smileys & Emotion",
      "subcategory": "",
      "keywords": ["visage", "sourire", "content"],
      "aliases": [":grinning:", ":D", ":-D"],
      "unicode": "U+1F600",
      "version": "1.0"
    }
  ],
  "categories": [
    {
      "id": "smileys-emotion",
      "name": "Smileys & Emotion",
      "icon": "😀",
      "count": 168,
      "order": 1
    }
  ]
}
```

## Génération des données

### Prérequis

- **Node.js 18+** installé
- Connexion Internet (pour télécharger depuis CLDR)

### Première génération

```bash
# 1. Aller dans le dossier scripts
cd scripts

# 2. Générer les données emoji
node generate-emoji-data.js
```

### Sortie attendue

```
📥 Téléchargement des annotations CLDR...
🔨 Génération des données emoji...
✅ 1847 emojis générés
📊 9 catégories
📁 Écriture dans ../src/IrcChat.Client/wwwroot/data/emojis.json
🎉 Terminé !

Statistiques par catégorie:
  😀 Smileys & Emotion: 168 emojis
  👋 People & Body: 421 emojis
  🐵 Animals & Nature: 154 emojis
  🍇 Food & Drink: 131 emojis
  ⚽ Activities: 89 emojis
  🌍 Travel & Places: 218 emojis
  💡 Objects: 265 emojis
  🔣 Symbols: 321 emojis
  🏁 Flags: 270 emojis
```

## Sources Unicode CLDR

### URLs officielles

- **Annotations françaises** : https://github.com/unicode-org/cldr-json (annotations/fr)
- **Annotations anglaises** : https://github.com/unicode-org/cldr-json (annotations/en)

### Catégories Unicode

Le script génère 9 catégories basées sur les groupes Unicode officiels :

1. **Smileys & Emotion** (U+1F600-U+1F64F, U+1F900-U+1F9FF)
2. **People & Body** (U+1F440-U+1F4FC, U+1F466-U+1F469)
3. **Animals & Nature** (U+1F300-U+1F5FF, U+1F400-U+1F43F)
4. **Food & Drink** (U+1F330-U+1F37F)
5. **Travel & Places** (U+1F680-U+1F6FF)
6. **Activities** (U+1F3A0-U+1F3CF)
7. **Objects** (U+1F3D0-U+1F3FF, U+1F4FD-U+1F53D, U+1FA70-U+1FAFF)
8. **Symbols** (U+2600-U+26FF, U+2700-U+27BF)
9. **Flags** (U+1F1E6-U+1F1FF)

### Aliases IRC classiques

Le script ajoute des aliases IRC classiques :

| Emoji | Code GitHub | Aliases IRC |
|-------|-------------|-------------|
| 😀    | :grinning:  | :D, :-D     |
| 😃    | :smile:     | :), :-)     |
| 😄    | :smiley:    | ^_^         |
| 😉    | :wink:      | ;), ;-)     |
| 😊    | :blush:     | ^^          |
| 😢    | :cry:       | :'(, :'-(   |
| 😂    | :joy:       | xD, XD      |
| 😮    | :open_mouth:| :o, :O, :-o |
| 😐    | :neutral:   | :\|, :-\|   |
| 😕    | :confused:  | :/, :-/     |
| 😡    | :rage:      | >:(, >:-(   |
| 😎    | :sunglasses:| B), B-)     |
| 😛    | :stuck_out_tongue: | :p, :P, :-p, :-P |
| 😜    | :stuck_out_tongue_winking_eye: | ;p, ;P, ;-p |
| 🤔    | :thinking:  | ?_?         |
| ❤️    | :heart:     | <3          |
| 💔    | :broken_heart: | </3      |
| 👍    | :thumbs_up: | +1          |
| 👎    | :thumbs_down: | -1        |

## Mise à jour des emojis

### Mise à jour manuelle

Pour mettre à jour avec la dernière version Unicode CLDR :

```bash
cd scripts
node generate-emoji-data.js
```

Puis commit le nouveau `emojis.json`.

### Fréquence recommandée

- **Après chaque release Unicode majeure** (annuelle, ~septembre)
- **Lors de l'ajout de nouveaux emojis populaires**
- **Corrections de traductions CLDR**

### Workflow GitHub Actions (optionnel)

Un workflow automatique peut être configuré pour vérifier les mises à jour mensuellement.

Voir `.github/workflows/update-emojis.yml` (si configuré).

## Intégration au build

### Option 1 : Génération manuelle (recommandé)

Générer manuellement avant chaque release majeure :

```bash
cd scripts
node generate-emoji-data.js
git add ../src/IrcChat.Client/wwwroot/data/emojis.json
git commit -m "chore: update emoji data"
```

### Option 2 : Pre-build automatique

Ajouter au `.csproj` pour génération automatique avant chaque build :

```xml
<Target Name="GenerateEmojiData" BeforeTargets="BeforeBuild">
  <Exec Command="node generate-emoji-data.js" 
        WorkingDirectory="$(ProjectDir)../../scripts"
        Condition="!Exists('$(ProjectDir)wwwroot/data/emojis.json')" />
</Target>
```

⚠️ **Note** : Génère uniquement si le fichier n'existe pas (évite la régénération à chaque build).

## Dépannage

### Erreur : "Cannot find module"

```bash
cd scripts
npm install  # Pas de dépendances normalement, mais au cas où
```

### Erreur : "ECONNREFUSED" ou timeout

Le script ne peut pas télécharger depuis GitHub. Vérifier :
- Connexion Internet active
- Pas de proxy bloquant GitHub
- URLs CLDR accessibles

### Fichier emojis.json non créé

Vérifier les permissions :
```bash
ls -la ../src/IrcChat.Client/wwwroot/data/
```

Créer le dossier si nécessaire :
```bash
mkdir -p ../src/IrcChat.Client/wwwroot/data/
```

### Emojis manquants ou incorrects

Vérifier les annotations CLDR source :
- https://github.com/unicode-org/cldr-json/tree/main/cldr-annotations-full/annotations

La qualité dépend de CLDR. Reporter les problèmes là-bas.

## Statistiques

### Taille des données

- **Fichier JSON** : ~500KB non compressé
- **Compressé (gzip)** : ~150KB
- **Nombre d'emojis** : ~1850 (Unicode 15.1)
- **Catégories** : 9

### Performance

- **Génération** : ~5-10 secondes (téléchargement + parsing)
- **Chargement client** : ~200ms (première fois, puis cached)
- **Recherche** : O(n) avec filtrage keywords (optimisé)
- **Conversion codes** : O(n) avec regex (rapide)

## Références

- **Unicode CLDR** : https://cldr.unicode.org/
- **Unicode Emoji** : https://unicode.org/emoji/
- **CLDR JSON** : https://github.com/unicode-org/cldr-json
- **Emoji Test** : https://unicode.org/Public/emoji/

## Support

Pour toute question :
1. Vérifier ce README
2. Consulter les issues GitHub
3. Créer une nouvelle issue avec le label `emoji`

---

**Dernière mise à jour** : Janvier 2025  
**Version Unicode** : 15.1  
**Emojis supportés** : 1847