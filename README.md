# IrcChat

[![Quality Gate Status](https://sonarcloud.io/api/project_badges/measure?project=aguacongas_IrcChat&metric=alert_status)](https://sonarcloud.io/summary/new_code?id=aguacongas_IrcChat)
[![Coverage](https://sonarcloud.io/api/project_badges/measure?project=aguacongas_IrcChat&metric=coverage)](https://sonarcloud.io/summary/new_code?id=aguacongas_IrcChat)
[![Bugs](https://sonarcloud.io/api/project_badges/measure?project=aguacongas_IrcChat&metric=bugs)](https://sonarcloud.io/summary/new_code?id=aguacongas_IrcChat)
[![Code Smells](https://sonarcloud.io/api/project_badges/measure?project=aguacongas_IrcChat&metric=code_smells)](https://sonarcloud.io/summary/new_code?id=aguacongas_IrcChat)
[![Security Rating](https://sonarcloud.io/api/project_badges/measure?project=aguacongas_IrcChat&metric=security_rating)](https://sonarcloud.io/summary/new_code?id=aguacongas_IrcChat)
[![Maintainability Rating](https://sonarcloud.io/api/project_badges/measure?project=aguacongas_IrcChat&metric=sqale_rating)](https://sonarcloud.io/summary/new_code?id=aguacongas_IrcChat)
[![Reliability Rating](https://sonarcloud.io/api/project_badges/measure?project=aguacongas_IrcChat&metric=reliability_rating)](https://sonarcloud.io/summary/new_code?id=aguacongas_IrcChat)
[![Duplicated Lines (%)](https://sonarcloud.io/api/project_badges/measure?project=aguacongas_IrcChat&metric=duplicated_lines_density)](https://sonarcloud.io/summary/new_code?id=aguacongas_IrcChat)

> 🤖 **Note importante** : Ce projet est principalement développé par **Claude (Anthropic)**. Mon rôle se limite à faire du code review et demander des corrections/améliorations. C'est une expérience de développement assisté par IA où l'assistant génère le code selon les spécifications fournies.

## 📝 Description

Application de chat IRC moderne développée avec .NET 10, Blazor WebAssembly et SignalR.

## 🏗️ Architecture

- **Backend** : ASP.NET Core 10.0 Web API
- **Frontend** : Blazor WebAssembly
- **Temps réel** : SignalR
- **Base de données** : PostgreSQL
- **Authentification** : JWT + OAuth 2.0 (Google, Facebook, Microsoft)
- **ORM** : Entity Framework Core

## 🚀 Fonctionnalités

### Chat en temps réel
- ✅ Canaux multiples
- ✅ Messages privés
- ✅ Historique des messages
- ✅ Utilisateurs connectés en temps réel

### Authentification
- ✅ JWT Authentication
- ✅ OAuth 2.0 (Google, Facebook, Microsoft)
- ✅ Gestion des sessions
- ✅ Réservation de pseudonymes avec historique conservé

### Administration
- ✅ Gestion des utilisateurs
- ✅ Gestion des canaux
- ✅ Réservation de noms d'utilisateur
- ✅ Bannissement d'utilisateurs

### Sécurité
- ✅ CORS configuré
- ✅ Rate limiting
- ✅ Validation des données
- ✅ Protection contre XSS/CSRF
- ✅ PKCE pour OAuth 2.0

## 🛠️ Technologies utilisées

### Backend
- .NET 10.0
- ASP.NET Core Web API
- SignalR
- Entity Framework Core
- PostgreSQL
- JWT Bearer Authentication

### Frontend
- Blazor WebAssembly
- HttpClient
- SignalR Client
- IndexedDB (pour stockage persistant)

### DevOps & Qualité
- GitHub Actions (CI/CD)
- SonarCloud (Analyse de code)
- Dependabot (Mises à jour automatiques)
- xUnit (Tests)
- bUnit (Tests Blazor)

## 📋 Prérequis

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [PostgreSQL 16+](https://www.postgresql.org/download/)
- [Visual Studio 2022](https://visualstudio.microsoft.com/) ou [VS Code](https://code.visualstudio.com/)
- [Node.js](https://nodejs.org/) (optionnel, pour Tailwind)
- Un fichier de licence [Six Labors](https://sixlabors.com/pricing/) (`sixlabors.lic`, requis pour la compilation)

## 🚀 Démarrage rapide

### 1. Cloner le repository

```bash
git clone https://github.com/VOTRE_USERNAME/IrcChat.git
cd IrcChat
```

### 2. Configuration de la base de données

```bash
# Créer la base de données PostgreSQL
createdb ircchat

# Configurer la connection string via user secrets
cd src/IrcChat.Api
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Database=ircchat;Username=postgres;Password=votre_password"
```

### 3. Configuration de la licence Six Labors

Ce projet utilise [SixLabors.ImageSharp](https://sixlabors.com/products/imagesharp/), qui nécessite une licence valide pour la compilation.

> ℹ️ Six Labors propose des licences gratuites pour les projets open source. Consultez [sixlabors.com/pricing](https://sixlabors.com/pricing/) pour les détails.

Placez votre fichier `sixlabors.lic` dans `src/IrcChat.Api/` :

```
src/IrcChat.Api/
├── sixlabors.lic   ← ici
├── IrcChat.Api.csproj
└── ...
```

SixLabors.ImageSharp détecte automatiquement le fichier au même niveau que le `.csproj` — aucun argument supplémentaire n'est nécessaire pour les commandes `dotnet build`, `dotnet test` ou `dotnet run`.

> ⚠️ **Ne commitez jamais** `sixlabors.lic`. Ajoutez cette ligne à votre `.gitignore` local :
> ```
> **/sixlabors.lic
> ```

### 4. Appliquer les migrations

```bash
cd src/IrcChat.Api
dotnet ef database update
```

### 5. Configuration OAuth (optionnel)

Pour configurer OAuth 2.0 avec Google, Microsoft et Facebook, suivre le guide complet :

📖 **[Configuration OAuth 2.0 - Guide complet](docs/OAUTH_SETUP.md)**

Résumé rapide :

```bash
# Google OAuth
dotnet user-secrets set "OAuth:Google:ClientId" "YOUR_CLIENT_ID"
dotnet user-secrets set "OAuth:Google:ClientSecret" "YOUR_CLIENT_SECRET"

# Microsoft OAuth
dotnet user-secrets set "OAuth:Microsoft:ClientId" "YOUR_CLIENT_ID"
dotnet user-secrets set "OAuth:Microsoft:ClientSecret" "YOUR_CLIENT_SECRET"

# Facebook OAuth
dotnet user-secrets set "OAuth:Facebook:AppId" "YOUR_APP_ID"
dotnet user-secrets set "OAuth:Facebook:AppSecret" "YOUR_APP_SECRET"
```

> ℹ️ Pour obtenir ces identifiants, consulter le [guide OAuth 2.0](docs/OAUTH_SETUP.md) qui explique comment les générer pour chaque provider.

### 6. Lancer l'application

```bash
# Terminal 1 - API
cd src/IrcChat.Api
dotnet run

# Terminal 2 - Client
cd src/IrcChat.Client
dotnet run
```

L'application sera accessible sur :
- **API** : https://localhost:7001
- **Client** : https://localhost:7002

## 🔑 Configuration des secrets CI/CD

### GitHub Actions

Créez les secrets suivants dans **Settings → Secrets and variables → Actions** :

| Secret | Description |
|--------|-------------|
| `SIXLABORS_LICENSE_FILE` | Contenu complet du fichier `sixlabors.lic` |
| `SONAR_TOKEN` | Token SonarCloud |

### Render.com

Dans **Environment → Build Environment Variables** :

| Variable | Description |
|----------|-------------|
| `SIXLABORS_LICENSE_FILE` | Contenu complet du fichier `sixlabors.lic` |

Le Dockerfile écrit le fichier à la volée dans le stage de build uniquement — il ne transite pas dans l'image finale.

### Build Docker local

Pour construire et lancer l'image Docker de l'API en local, passez le contenu du fichier `sixlabors.lic` comme argument de build :

```bash
# Linux / macOS
docker build \
  --build-arg SIXLABORS_LICENSE_FILE="$(cat src/IrcChat.Api/sixlabors.lic)" \
  -t ircchat-api \
  -f src/IrcChat.Api/Dockerfile \
  .

# Windows (PowerShell)
docker build `
  --build-arg SIXLABORS_LICENSE_FILE="$(Get-Content src/IrcChat.Api/sixlabors.lic -Raw)" `
  -t ircchat-api `
  -f src/IrcChat.Api/Dockerfile `
  .
```

Puis lancer le conteneur :

```bash
docker run -p 8080:8080 \
  -e ConnectionStrings__DefaultConnection="Host=host.docker.internal;Database=ircchat;Username=postgres;Password=votre_password" \
  ircchat-api
```

> ℹ️ Sur Linux, remplacez `host.docker.internal` par l'IP de votre machine hôte (ex: `172.17.0.1`) si PostgreSQL tourne en dehors de Docker.

## 🧪 Tests

### Lancer tous les tests

```bash
dotnet test
```

### Tests avec couverture

```bash
dotnet test --collect:"XPlat Code Coverage" -- DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Format=opencover
```

### Mode watch (développement)

```bash
# Windows
.\watch-tests.ps1

# Ou manuellement
dotnet watch test --project tests/IrcChat.Api.Tests/IrcChat.Api.Tests.csproj
```

### Objectifs de couverture

- **Global** : ≥ 80%
- **API** : ≥ 80%
- **Client** : ≥ 70%
- **Logique critique** : 100%

## 📁 Structure du projet

```
IrcChat/
├── .github/
│   └── workflows/           # GitHub Actions
│       ├── pr-checks.yml           # Vérifications PR
│       ├── sonar-main-analysis.yml # Analyse SonarCloud
│       └── release.yml             # Release automatique
├── docs/
│   ├── OAUTH_SETUP.md      # 📖 Configuration OAuth 2.0
│   └── ...
├── src/
│   ├── IrcChat.Api/        # Backend API
│   │   ├── Controllers/
│   │   ├── Data/
│   │   ├── Endpoints/      # 🆕 Endpoints API (minimal API)
│   │   │   ├── AdminManagementEndpoints.cs
│   │   │   ├── ChannelDeleteEndpoints.cs
│   │   │   ├── ChannelEndpoints.cs
│   │   │   ├── ChannelMuteEndpoints.cs
│   │   │   ├── MessageEndpoints.cs
│   │   │   ├── OAuthEndpoints.cs
│   │   │   └── PrivateMessageEndpoints.cs
│   │   ├── Extensions/     # Extension methods
│   │   │   ├── ConnectionManagerOptionsExtensions.cs
│   │   │   ├── ServiceCollectionExtensions.cs
│   │   │   └── WebApplicationExtensions.cs
│   │   ├── Hubs/          # SignalR Hubs
│   │   ├── Migrations/
│   │   ├── Models/
│   │   └── Services/
│   ├── IrcChat.Client/     # Frontend Blazor
│   │   ├── Pages/
│   │   ├── Shared/
│   │   └── Services/
│   └── IrcChat.Shared/     # DTOs partagés
│       └── Models/
└── tests/
    ├── IrcChat.Api.Tests/      # Tests backend
    │   ├── Integration/
    │   ├── Services/
    │   └── Hubs/
    └── IrcChat.Client.Tests/   # Tests frontend
        ├── Pages/
        └── Components/
```

## 📖 Documentation

- [Directives de codage](CODING_GUIDELINES.md)
- [Politique de tests](TEST_POLICY.md)
- [Bonnes pratiques de test](TESTING_BEST_PRACTICES.md)
- [Configuration SonarCloud](SONARCLOUD_SETUP.md)
- [Configuration OAuth 2.0](docs/OAUTH_SETUP.md) 🆕

## 🔄 Workflow CI/CD

### Pull Request
1. ✅ Build & Tests
2. ✅ Analyse SonarCloud (Quality Gate)
3. ✅ Vérification du style de code
4. ✅ Vérification des migrations
5. ✅ Review des dépendances

### Merge sur main
1. ✅ Analyse SonarCloud complète
2. ✅ Mise à jour des métriques
3. ✅ Archivage des rapports de couverture

### Release (tags v*.*.*)
1. ✅ Build Release
2. ✅ Tests complets
3. ✅ Publication des artifacts
4. ✅ Génération du changelog
5. ✅ Création de la release GitHub

## 🤝 Contribution

Ce projet suit une approche particulière :

1. **Code généré par Claude** : L'essentiel du code est écrit par Claude (Anthropic)
2. **Review humaine** : Je fais du code review et demande des corrections
3. **Standards stricts** : Respect des guidelines de codage C# et des bonnes pratiques
4. **Tests obligatoires** : Chaque fonctionnalité doit avoir ses tests
5. **Qualité vérifiée** : SonarCloud valide la qualité du code

### Processus

1. Je décris la fonctionnalité souhaitée à Claude
2. Claude génère le code et les tests
3. Je review et demande des ajustements si nécessaire
4. Les GitHub Actions valident automatiquement
5. Merge après validation du Quality Gate

## 🤖 Développement assisté par IA

Ce projet utilise Claude (Anthropic) en **mode Socratic** pour la génération de code.

### Configuration

La configuration de Claude se trouve dans `.claude/project-config.md` et définit :
- Les questions à poser avant de générer du code
- Les patterns obligatoires à respecter
- Le processus de validation en 4 étapes
- Les standards de qualité requis

### Utilisation

Pour générer du code avec Claude :
```
Mode Socratic : [ta demande]
```

Claude posera des questions de clarification, présentera un plan, puis générera le code après validation.

Voir [.claude/project-config.md](.claude/project-config.md) pour plus de détails.

[![Developed with Claude](https://img.shields.io/badge/Developed%20with-Claude-5A67D8?style=flat-square&logo=anthropic)](https://www.anthropic.com/claude)
[![Socratic Mode](https://img.shields.io/badge/Mode-Socratic-orange?style=flat-square)](.claude/project-config.md)

## 📜 Licence

Ce projet est sous licence [Apache 2.0](LICENSE.txt).

## 🙏 Remerciements

- **Claude (Anthropic)** pour la génération du code
- **SonarCloud** pour l'analyse de qualité
- **GitHub Actions** pour le CI/CD
- La communauté .NET et Blazor

## 📞 Contact

Pour toute question ou suggestion, n'hésitez pas à ouvrir une issue.

---

> 💡 **Expérience de développement assisté par IA** : Ce projet démontre comment un développeur humain peut collaborer efficacement avec une IA (Claude) pour créer une application complète. L'humain définit les spécifications et assure la qualité, tandis que l'IA génère le code selon les standards définis.
