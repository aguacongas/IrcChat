const https = require('https');
const fs = require('fs');
const path = require('path');

// URLs CLDR
const CLDR_ANNOTATIONS_FR = 'https://raw.githubusercontent.com/unicode-org/cldr-json/refs/heads/main/cldr-json/cldr-annotations-full/annotations/fr/annotations.json';
const CLDR_ANNOTATIONS_EN = 'https://raw.githubusercontent.com/unicode-org/cldr-json/refs/heads/main/cldr-json/cldr-annotations-full/annotations/en/annotations.json';

// Mapping catégories Unicode → noms affichables
const CATEGORY_MAPPING = {
  'Smileys & Emotion': { icon: '😀', order: 1 },
  'People & Body': { icon: '👋', order: 2 },
  'Animals & Nature': { icon: '🐵', order: 3 },
  'Food & Drink': { icon: '🍇', order: 4 },
  'Travel & Places': { icon: '🌍', order: 5 },
  'Activities': { icon: '⚽', order: 6 },
  'Objects': { icon: '💡', order: 7 },
  'Symbols': { icon: '🔣', order: 8 },
  'Flags': { icon: '🏁', order: 9 }
};

// Aliases IRC classiques
const IRC_ALIASES = {
  '😀': [':D', ':-D'],
  '😃': [':)', ':-)'],
  '😄': ['^_^'],
  '😉': [';)', ';-)'],
  '😊': ['^^'],
  '😢': [":'(", ":'-("],
  '😂': ['xD', 'XD'],
  '😮': [':o', ':O', ':-o'],
  '😐': [':|', ':-|'],
  '😕': [':/', ':-/'],
  '😡': ['>:(', '>:-('],
  '😎': ['B)', 'B-)'],
  '😛': [':p', ':P', ':-p', ':-P'],
  '😜': [';p', ';P', ';-p'],
  '🤔': ['?_?'],
  '❤️': ['<3'],
  '💔': ['</3'],
  '👍': ['+1'],
  '👎': ['-1']
};

async function fetchJSON(url) {
  return new Promise((resolve, reject) => {
    https.get(url, (res) => {
      let data = '';
      res.on('data', chunk => data += chunk);
      res.on('end', () => {
        try {
          resolve(JSON.parse(data));
        } catch (error) {
          reject(error);
        }
      });
      res.on('error', reject);
    }).on('error', reject);
  });
}

function generateGitHubCode(nameEn) {
  // Génère :code: depuis le nom anglais
  const code = nameEn
    .toLowerCase()
    .replace(/[^a-z0-9\s]/g, '')
    .trim()
    .replace(/\s+/g, '_');
  return `:${code}:`;
}

function inferCategory(emoji) {
  // Logique simplifiée basée sur les code points Unicode
  const codePoint = emoji.codePointAt(0);
  
  if (codePoint >= 0x1F600 && codePoint <= 0x1F64F) return 'Smileys & Emotion';
  if (codePoint >= 0x1F680 && codePoint <= 0x1F6FF) return 'Travel & Places';
  if (codePoint >= 0x1F300 && codePoint <= 0x1F5FF) return 'Animals & Nature';
  if (codePoint >= 0x1F900 && codePoint <= 0x1F9FF) return 'Smileys & Emotion';
  if (codePoint >= 0x1FA70 && codePoint <= 0x1FAFF) return 'Objects';
  if (codePoint >= 0x2600 && codePoint <= 0x26FF) return 'Symbols';
  if (codePoint >= 0x2700 && codePoint <= 0x27BF) return 'Symbols';
  if (codePoint >= 0x1F1E6 && codePoint <= 0x1F1FF) return 'Flags';
  if (codePoint >= 0x1F400 && codePoint <= 0x1F4FF) return 'Animals & Nature';
  if (codePoint >= 0x1F330 && codePoint <= 0x1F37F) return 'Food & Drink';
  if (codePoint >= 0x1F380 && codePoint <= 0x1F3FF) return 'Activities';
  if (codePoint >= 0x1F3A0 && codePoint <= 0x1F3CF) return 'Activities';
  if (codePoint >= 0x1F3D0 && codePoint <= 0x1F3FF) return 'Objects';
  if (codePoint >= 0x1F400 && codePoint <= 0x1F43F) return 'Animals & Nature';
  if (codePoint >= 0x1F440 && codePoint <= 0x1F4FC) return 'People & Body';
  if (codePoint >= 0x1F4FD && codePoint <= 0x1F53D) return 'Objects';
  if (codePoint >= 0x1F54B && codePoint <= 0x1F67F) return 'Objects';
  if (codePoint >= 0x1F466 && codePoint <= 0x1F469) return 'People & Body';
  
  return 'Symbols';
}

function getUnicodeCodepoint(emoji) {
  return Array.from(emoji)
    .map(char => `U+${char.codePointAt(0).toString(16).toUpperCase().padStart(4, '0')}`)
    .join(' ');
}

function getUnicodeVersion(emoji) {
  // Simplifié - déterminer la version Unicode
  const codePoint = emoji.codePointAt(0);
  if (codePoint <= 0x1F64F) return '6.0';
  if (codePoint <= 0x1F6FF) return '7.0';
  if (codePoint <= 0x1F9FF) return '11.0';
  if (codePoint <= 0x1FAFF) return '13.0';
  return '14.0';
}

async function generateEmojiData() {
  console.log('📥 Téléchargement des annotations CLDR...');
  
  try {
    const annotationsFr = await fetchJSON(CLDR_ANNOTATIONS_FR);
    const annotationsEn = await fetchJSON(CLDR_ANNOTATIONS_EN);
    
    console.log('🔨 Génération des données emoji...');
    
    const emojis = [];
    const categories = new Map();
    
    // Parser les annotations
    const annotationsDataFr = annotationsFr.annotations.annotations;
    const annotationsDataEn = annotationsEn.annotations.annotations;
    
    for (const [emoji, dataEn] of Object.entries(annotationsDataEn)) {
      // Filtrer les emojis simples (pas les séquences trop complexes)
      if (emoji.length > 10) continue;
      
      const dataFr = annotationsDataFr[emoji];
      if (!dataFr) continue;
      
      const nameEn = dataEn.tts?.[0] || '';
      const nameFr = dataFr.tts?.[0] || nameEn;
      
      if (!nameEn) continue;
      
      const keywordsFr = dataFr.default || [];
      const keywordsEn = dataEn.default || [];
      const keywords = [...new Set([...keywordsFr, ...keywordsEn])];
      
      // Déterminer la catégorie
      const category = inferCategory(emoji);
      
      const code = generateGitHubCode(nameEn);
      const aliases = IRC_ALIASES[emoji] || [];
      
      const emojiItem = {
        emoji,
        code,
        name: nameFr,
        nameEn,
        category,
        subcategory: '',
        keywords,
        aliases: [...new Set([code, ...aliases])],
        unicode: getUnicodeCodepoint(emoji),
        version: getUnicodeVersion(emoji)
      };
      
      emojis.push(emojiItem);
      
      // Compter par catégorie
      if (!categories.has(category)) {
        categories.set(category, {
          id: category.toLowerCase().replace(/\s+/g, '-').replace(/&/g, 'and'),
          name: category,
          icon: CATEGORY_MAPPING[category]?.icon || emoji,
          count: 0,
          order: CATEGORY_MAPPING[category]?.order || 99
        });
      }
      categories.get(category).count++;
    }
    
    const result = {
      version: '15.1',
      generatedAt: new Date().toISOString(),
      emojis: emojis.sort((a, b) => a.unicode.localeCompare(b.unicode)),
      categories: Array.from(categories.values()).sort((a, b) => a.order - b.order)
    };
    
    console.log(`✅ ${emojis.length} emojis générés`);
    console.log(`📊 ${categories.size} catégories`);
    
    // Déterminer le chemin de sortie
    const outputDir = path.join(__dirname, '..', 'src', 'IrcChat.Client', 'wwwroot', 'data');
    const outputFile = path.join(outputDir, 'emojis.json');
    
    console.log(`📁 Écriture dans ${outputFile}`);
    
    // Créer le dossier si nécessaire
    if (!fs.existsSync(outputDir)) {
      fs.mkdirSync(outputDir, { recursive: true });
    }
    
    fs.writeFileSync(outputFile, JSON.stringify(result, null, 2));
    
    console.log('🎉 Terminé !');
    console.log('\nStatistiques par catégorie:');
    Array.from(categories.values())
      .sort((a, b) => a.order - b.order)
      .forEach(cat => {
        console.log(`  ${cat.icon} ${cat.name}: ${cat.count} emojis`);
      });
    
  } catch (error) {
    console.error('❌ Erreur:', error.message);
    process.exit(1);
  }
}

// Exécution
generateEmojiData();