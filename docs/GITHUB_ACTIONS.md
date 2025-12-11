# GitHub Actions - IrcChat

> 🤖 **Note** : Ce projet utilise plusieurs workflows GitHub Actions pour automatiser les tâches de CI/CD, qualité de code, et maintenance.

## 📋 Table des matières

- [Vue d'ensemble](#-vue-densemble)
- [Workflows disponibles](#-workflows-disponibles)
  - [Pull Request Checks](#-pull-request-checks)
  - [SonarCloud Main Analysis](#-sonarcloud-main-analysis)
  - [Release](#-release)
  - [Auto Label PR](#-auto-label-pr)
  - [Update Emoji Data](#-update-emoji-data)
  - [Deploy to GitHub Pages](#-deploy-to-github-pages)
- [Configuration](#-configuration)
- [Templates](#-templates)
- [Dépannage](#-dépannage)

---

## 🎯 Vue d'ensemble

Le projet utilise **7 workflows GitHub Actions** :

| Workflow | Déclencheur | Objectif |
|----------|-------------|----------|
| **PR Checks** | Pull Request sur `main` | Validation complète (build, tests, SonarCloud) |
| **SonarCloud Analysis** | Push sur `main` | Analyse de qualité du code |
| **Release** | Push d'un tag `v*.*.*` | Création d'une release avec artifacts |
| **Auto Label PR** | Ouverture/mise à jour PR | Labeling automatique selon commits |
| **Update Emojis** | Manuel / Mensuel | Mise à jour des données emoji CLDR |
| **Deploy to Pages** | Push sur `main` / Manuel | Déploiement sur GitHub Pages |
| **Dependabot** | Automatique | Mise à jour des dépendances |

---

## 📦 Workflows disponibles

### 🔍 Pull Request Checks

**Fichier** : `.github/workflows/pr-checks.yml`

**Déclencheur** : Pull Request vers `main`

#### Jobs

**1. validate-pr**
- 🔧 Setup .NET 10
- 🔍 Vérification du style de code (`dotnet format`)
- 🏗️ Build Release
- 🧪 Tests unitaires (API + Client)
- 📊 Rapport de tests dans la PR
- 🔍 Analyse SonarCloud
- ✅ Quality Gate check

**2. check-migrations**
- 🗄️ Test des migrations EF Core
- 🐘 PostgreSQL 16 en service
- ✅ Vérification de l'état de la DB

**3. dependency-review**
- 🔍 Review des dépendances
- ⚠️ Alerte sur vulnérabilités ≥ moderate

**4. size-label**
- 🏷️ Labeling automatique selon la taille de la PR
  - `size/xs` : ≤ 10 lignes
  - `size/s` : ≤ 100 lignes
  - `size/m` : ≤ 500 lignes
  - `size/l` : ≤ 1000 lignes
  - `size/xl` : > 1000 lignes

#### Permissions requises
```yaml
issues: write
pull-requests: write
contents: read
actions: read
checks: write
```

#### Variables secrets requises
- `SONAR_TOKEN` : Token SonarCloud
- `SONAR_PROJECT_KEY` (var) : Clé du projet SonarCloud
- `SONAR_ORGANIZATION` (var) : Organisation SonarCloud

---

### 📊 SonarCloud Main Analysis

**Fichier** : `.github/workflows/sonar-main-analysis.yml`

**Déclencheur** : Push sur `main`

#### Fonctionnalités
- Analyse complète du code sur la branche principale
- Couverture de code avec OpenCover
- Exclusions : Migrations, Tests, wwwroot
- Upload des rapports de couverture (conservés 30 jours)

#### Configuration SonarCloud
```yaml
/d:sonar.coverage.exclusions="**/Migrations/**,**/*Tests/**,**/wwwroot/**"
/d:sonar.exclusions="**/Migrations/**,**/wwwroot/lib/**,**/obj/**,**/bin/**"
```

#### Artifacts générés
- `coverage-reports` : Rapports OpenCover XML (30 jours)

---

### 🎉 Release

**Fichier** : `.github/workflows/release.yml`

**Déclencheur** : Push d'un tag `v*.*.*` (ex: `v1.2.3`)

#### Processus
1. 🔧 Build Release
2. 🧪 Tests complets
3. 📦 Publication API (single file, trimmed)
4. 📦 Publication Client (Blazor compressed)
5. 📋 Génération du changelog depuis les PRs
6. 🎉 Création de la release GitHub

#### Artifacts générés
- `ircchat-api-{version}.zip` - Backend API
- `ircchat-client-{version}.zip` - Frontend Blazor

#### Changelog automatique

Le changelog est généré depuis les labels des PRs mergées :

| Label | Section du changelog |
|-------|---------------------|
| `enhancement`, `feature` | ✨ Features |
| `bug`, `fix` | 🐛 Bug Fixes |
| `documentation`, `docs` | 📝 Documentation |
| `test`, `tests` | 🧪 Tests |
| `chore`, `dependencies` | 🔧 Maintenance |
| `performance`, `perf` | 🚀 Performance |
| `security` | 🔒 Security |
| `changelog:exclude` | Exclu du changelog |

#### Prerelease automatique

Les tags contenant `-alpha`, `-beta` ou `-rc` créent une prerelease :
```bash
v1.2.0-alpha.1  # Prerelease
v1.2.0-beta.1   # Prerelease
v1.2.0-rc.1     # Prerelease
v1.2.0          # Release stable
```

#### Créer une release

```bash
# 1. Créer et push le tag
git tag v1.2.0
git push origin v1.2.0

# 2. Le workflow démarre automatiquement
# 3. La release apparaît dans GitHub Releases
```

---

### 🏷️ Auto Label PR

**Fichier** : `.github/workflows/auto-label-pr.yml`

**Déclencheur** : Ouverture/mise à jour d'une PR

#### Fonctionnalités

Analyse les messages de commits et ajoute automatiquement des labels selon les **Conventional Commits** :

| Format commit | Labels ajoutés |
|---------------|----------------|
| `feat:` ou `feat(...)` | `feature`, `enhancement` |
| `fix:` ou `fix(...)` | `bug`, `fix` |
| `docs:` ou `docs(...)` | `documentation`, `docs` |
| `test:` ou `test(...)` | `test`, `tests` |
| `perf:` ou `perf(...)` | `performance`, `perf` |
| `refactor:` | `refactor` |
| `style:` | `style` |
| `chore:` | `chore` |
| Contient `dependencies` | `dependencies` |
| Contient `security` | `security` |
| Contient `breaking change` ou `!:` | `breaking-change` |

#### Comportement

**Si commits conventionnels détectés** :
- ✅ Ajoute les labels appropriés
- 💬 Commente la PR avec la liste des labels
- 📋 Guide pour modifier les labels si nécessaire

**Si AUCUN commit conventionnel** :
- ⚠️ Ajoute le label `changelog:exclude`
- 💬 Commente avec un avertissement
- 📝 Fournit des exemples de commits conventionnels
- 💡 Explique comment inclure dans le changelog

#### Exemple de commentaire

```markdown
🤖 **Labels ajoutés automatiquement** : `feature`, `enhancement`

Basé sur l'analyse des messages de commits.

💡 **Vous pouvez modifier ces labels** avant de merger la PR.

📋 Labels disponibles pour le changelog :
- `feature`, `enhancement` → ✨ Features
- `bug`, `fix` → 🐛 Bug Fixes
- `documentation`, `docs` → 📝 Documentation
- `test`, `tests` → 🧪 Tests
- `chore`, `dependencies` → 🔧 Maintenance
- `performance`, `perf` → 🚀 Performance
- `security` → 🔒 Security
```

---

### 🔄 Update Emoji Data

**Fichier** : `.github/workflows/update-emojis.yml`

**Déclencheur** :
- 🖱️ Manuel (workflow_dispatch)
- 📅 Automatique le 1er de chaque mois à 2h UTC
- 🏷️ Sur push de tags `v*.*.*` (optionnel)

#### Processus
1. 📥 Checkout du code
2. 🔧 Setup Node.js 20
3. 🔨 Génération de `emojis.json` depuis Unicode CLDR
4. 🔍 Détection des changements
5. 📊 Extraction des statistiques (version, count)
6. 📝 Création d'une PR si changements détectés

#### Pull Request générée

**Si changements détectés** :
- Titre : `🔄 Update Emoji Data (Unicode 15.1)`
- Labels : `automated`, `emojis`, `dependencies`
- Branche : `update-emojis-{run_number}`
- Suppression automatique de la branche après merge

**Si aucun changement** :
- ℹ️ Message dans le summary
- Aucune PR créée

#### Utilisation manuelle

1. Aller dans **Actions** → **Update Emoji Data**
2. Cliquer **Run workflow**
3. Sélectionner la branche (généralement `main`)
4. Attendre ~1 minute
5. Merger la PR si créée

#### Configuration de la fréquence

Modifier la ligne `cron` pour changer la fréquence :

```yaml
schedule:
  - cron: '0 2 1 * *'  # 1er du mois à 2h
```

**Exemples** :
```yaml
- cron: '0 2 * * 1'   # Tous les lundis à 2h
- cron: '0 2 1 */3 *' # 1er du mois tous les 3 mois
- cron: '0 2 15 * *'  # Le 15 de chaque mois à 2h
```

---

### 🌐 Deploy to GitHub Pages

**Fichier** : `.github/workflows/deploy-to-github-pages.yml`

**Déclencheur** :
- Push sur `main`
- Manuel (workflow_dispatch)

#### Processus
1. 🔧 Setup .NET 10.0
2. 🏗️ Build Release
3. 📦 Publish Blazor WebAssembly
4. 🔧 Fix du base path pour GitHub Pages (`/IrcChat/`)
5. 📄 Ajout du fichier `.nojekyll`
6. 🚀 Déploiement sur GitHub Pages

#### Configuration requise

**1. Activer GitHub Pages**
- Repository → Settings → Pages
- Source : GitHub Actions
- Branch : (géré par l'action)

**2. Permissions**
```yaml
permissions:
  contents: read
  pages: write
  id-token: write
```

#### URL de déploiement

L'application sera accessible à :
```
https://{username}.github.io/IrcChat/
```

---

## 🤖 Dependabot

**Fichier** : `.github/dependabot.yml`

**Déclencheur** : Automatique tous les lundis à 9h-11h

#### Ecosystèmes surveillés

1. **NuGet** (5 configs)
   - API (`/src/IrcChat.Api`)
   - Client (`/src/IrcChat.Client`)
   - Shared (`/src/IrcChat.Shared`)
   - Tests API (`/tests/IrcChat.Api.Tests`)
   - Tests Client (`/tests/IrcChat.Client.Tests`)

2. **GitHub Actions** (`/`)
   - Mise à jour des actions utilisées

3. **Docker** (`/src/IrcChat.Api`)
   - Images de base dans les Dockerfiles

#### Configuration

- **Fréquence** : Hebdomadaire (lundi)
- **Limite PR** : 10 pour NuGet, 5 pour Actions/Tests, 3 pour Docker
- **Labels** : `dependencies` + type spécifique
- **Commits** : Format conventionnel (`chore(api)`, `chore(client)`)

#### Personnalisation

Modifier le nombre max de PRs :
```yaml
open-pull-requests-limit: 5  # Par défaut : 10
```

Changer la fréquence :
```yaml
schedule:
  interval: "daily"  # daily, weekly, monthly
  day: "tuesday"     # Pour weekly
  time: "10:00"      # Format 24h
```

---

## 📝 Templates

### Issue Templates

Le projet utilise des templates YAML pour les issues :

#### 1. 🐛 Bug Report
**Fichier** : `.github/ISSUE_TEMPLATE/bug_report.yml`

**Champs** :
- Description du bug
- Étapes de reproduction
- Comportement attendu/actuel
- Composant affecté (dropdown)
- Sévérité (dropdown)
- Logs/Screenshots
- Version et environnement

#### 2. ✨ Feature Request
**Fichier** : `.github/ISSUE_TEMPLATE/feature_request.yml`

**Champs** :
- Problème à résoudre
- Solution proposée
- Composant concerné
- Priorité
- Alternatives
- Critères d'acceptation
- Spécifications techniques
- UI/UX Mockup
- Exigences de test
- Breaking changes
- Documentation nécessaire

#### 3. ❓ Question
**Fichier** : `.github/ISSUE_TEMPLATE/question.yml`

**Champs** :
- Catégorie (dropdown)
- Question
- Contexte
- Ce qui a été essayé

#### Configuration
**Fichier** : `.github/ISSUE_TEMPLATE/config.yml`

**Liens utiles** :
- 📖 Documentation
- 💬 Discussions
- 🤖 À propos de Claude
- 📊 SonarCloud

### Pull Request Template

**Fichier** : `.github/PULL_REQUEST_TEMPLATE.md`

**Sections** :
- Description
- Génération (code généré par Claude)
- Type de changement
- Checklist complète (code, tests, docs, qualité)
- Tests effectués
- Couverture de code
- Issues liées
- Screenshots
- Déploiement (migrations, env vars)
- Notes supplémentaires
- Review checklist

---

## ⚙️ Configuration

### Secrets requis

Aller dans **Settings → Secrets and variables → Actions**

#### Secrets
- `SONAR_TOKEN` : Token SonarCloud (obligatoire pour analyse)
- `GITHUB_TOKEN` : Fourni automatiquement par GitHub

#### Variables
- `SONAR_PROJECT_KEY` : Clé du projet sur SonarCloud
- `SONAR_ORGANIZATION` : Organisation SonarCloud

### Permissions des workflows

Les workflows nécessitent ces permissions (configurées dans les fichiers `.yml`) :

```yaml
permissions:
  contents: write        # Pour créer des releases
  pull-requests: write   # Pour commenter les PRs
  issues: write          # Pour créer/modifier des issues
  pages: write           # Pour déployer sur Pages
  id-token: write        # Pour l'authentification Pages
  actions: read          # Pour lire les artifacts
  checks: write          # Pour publier les résultats de tests
```

### Variables d'environnement

Communes à tous les workflows :
```yaml
env:
  DOTNET_VERSION: '10.0.x'
```

---

## 🐛 Dépannage

### Erreur : "SonarCloud Quality Gate failed"

**Cause** : La qualité du code ne passe pas le Quality Gate SonarCloud.

**Solutions** :
1. Consulter le rapport SonarCloud (lien dans les logs)
2. Corriger les bugs/code smells critiques
3. Améliorer la couverture de code si < 80%

### Erreur : "dotnet format --verify-no-changes failed"

**Cause** : Le code n'est pas formaté selon les règles.

**Solution** :
```bash
dotnet format
git add .
git commit -m "style: format code"
```

### Erreur : "Database migration failed"

**Cause** : Migration EF Core invalide ou non applicable.

**Solutions** :
1. Vérifier la migration localement :
   ```bash
   dotnet ef database update
   ```
2. Corriger les erreurs de migration
3. Tester sur une DB vide

### Erreur : "Coverage reports not found"

**Cause** : Les fichiers de couverture ne sont pas générés correctement.

**Solution** : Vérifier que les tests utilisent bien :
```bash
--collect:"XPlat Code Coverage"
-- DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Format=opencover
```

### Erreur : "Quality Gate check timed out"

**Cause** : SonarCloud met trop de temps à analyser.

**Solution** : Augmenter le timeout dans le workflow :
```yaml
timeout-minutes: 10  # Au lieu de 5
```

### PR auto-labeling ne fonctionne pas

**Cause** : Les commits ne suivent pas le format Conventional Commits.

**Solutions** :
1. Utiliser le format : `type: description` ou `type(scope): description`
2. Types reconnus : `feat`, `fix`, `docs`, `test`, `chore`, `perf`, `refactor`, `style`
3. Ou ajouter manuellement les labels

### Dependabot ne crée pas de PRs

**Causes possibles** :
1. Limite de PRs atteinte (`open-pull-requests-limit`)
2. Pas de mises à jour disponibles
3. Dependabot désactivé dans les settings

**Solution** : Aller dans **Insights → Dependency graph → Dependabot** pour voir le statut.

---

## 📊 Bonnes pratiques

### Pour les PRs

1. **Utiliser Conventional Commits** pour l'auto-labeling :
   ```bash
   feat: add user ban system
   fix: correct message display bug
   docs: update README with OAuth setup
   test: add integration tests for channels
   ```

2. **Vérifier localement avant de push** :
   ```bash
   dotnet format --verify-no-changes
   dotnet build
   dotnet test
   ```

3. **Ajouter les bons labels** si l'auto-labeling échoue

4. **Écrire des descriptions claires** dans la PR

### Pour les releases

1. **Créer des tags sémantiques** :
   ```bash
   v1.0.0     # Major release
   v1.1.0     # Minor release (new features)
   v1.1.1     # Patch release (bug fixes)
   v1.2.0-rc.1  # Release candidate (prerelease)
   ```

2. **Merger les PRs avec les bons labels** avant de créer le tag

3. **Vérifier le changelog généré** avant de publier

### Pour les dépendances

1. **Merger les PRs Dependabot régulièrement**
2. **Tester après chaque merge de dépendance**
3. **Grouper les mises à jour mineures**
4. **Traiter les mises à jour de sécurité en priorité**

---

## 🔗 Ressources

### Documentation externe
- [GitHub Actions Documentation](https://docs.github.com/en/actions)
- [Conventional Commits](https://www.conventionalcommits.org/)
- [SonarCloud Documentation](https://docs.sonarcloud.io/)
- [Dependabot Documentation](https://docs.github.com/en/code-security/dependabot)

### Liens du projet
- [SonarCloud Dashboard](https://sonarcloud.io/project/overview?id=aguacongas_IrcChat)
- [Repository GitHub](https://github.com/aguacongas/IrcChat)
- [Documentation principale](../README.md)

---

## 📝 Changelog des workflows

### v1.0 (Décembre 2024)
- ✅ PR Checks complet avec SonarCloud
- ✅ Release automatique avec changelog
- ✅ Auto-labeling des PRs
- ✅ Update emoji automatique
- ✅ Deploy GitHub Pages
- ✅ Dependabot configuré
- ✅ Templates d'issues YAML

---

**Dernière mise à jour** : Décembre 2024  
**Auteur** : IrcChat Team (Code généré par Claude)