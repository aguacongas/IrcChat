# Guide d'intégration SonarCloud

## Configuration initiale

### 1. Créer un compte SonarCloud

1. Aller sur [sonarcloud.io](https://sonarcloud.io)
2. Se connecter avec votre compte GitHub
3. Autoriser l'accès à votre organisation GitHub

### 2. Configurer le projet

1. Cliquer sur **"+"** → **"Analyze new project"**
2. Sélectionner votre repository **IrcChat**
3. Choisir **"With GitHub Actions"**
4. Noter votre **Organization Key** et **Project Key**

### 3. Configurer les secrets et variables GitHub

Dans votre repository GitHub :

#### Secrets (Settings → Secrets and variables → Actions → Secrets)
1. Ajouter un nouveau secret :
   - Nom : `SONAR_TOKEN`
   - Valeur : Le token généré par SonarCloud

Pour obtenir le token :
1. Sur SonarCloud : **Account** → **Security** → **Generate Tokens**
2. Nom : `GitHub Actions`
3. Type : `User Token` ou `Project Analysis Token`
4. Copier le token généré

#### Variables (Settings → Secrets and variables → Actions → Variables)
1. Ajouter deux nouvelles variables :
   - Nom : `SONAR_PROJECT_KEY`
   - Valeur : Votre project key (ex: `username_IrcChat`)
   
   - Nom : `SONAR_ORGANIZATION`
   - Valeur : Votre organization (ex: `username`)

### 4. Vérifier la configuration

Le workflow `pr-checks.yml` a été mis à jour pour intégrer SonarCloud :
- ✅ Utilise les variables `${{ vars.SONAR_PROJECT_KEY }}` et `${{ vars.SONAR_ORGANIZATION }}`
- ✅ Utilise le secret `${{ secrets.SONAR_TOKEN }}`
- ✅ Génère la couverture de code au format OpenCover
- ✅ Vérifie le Quality Gate automatiquement

## Ce qui a été intégré

### Dans `.github/workflows/pr-checks.yml`

Le job `validate-pr` inclut maintenant :
1. **Setup Java 17** : Nécessaire pour SonarScanner
2. **Begin SonarCloud analysis** : Démarre l'analyse avec les variables du repo
3. **Build & Tests** : Avec génération de couverture OpenCover
4. **End SonarCloud analysis** : Termine l'analyse et envoie les résultats
5. **Quality Gate check** : Vérifie que le Quality Gate passe

### Dans `sonar-project.properties`

Configuration simplifiée :
- Les clés `projectKey` et `organization` sont passées via le workflow
- Exclusions intelligentes (migrations, tests, Program.cs)
- Configuration de la couverture de code

## Utilisation

### Déclencher une analyse

L'analyse se déclenche automatiquement sur :
- ✅ Pull Request vers `main`

### Vérifier les résultats

1. Aller sur [sonarcloud.io](https://sonarcloud.io)
2. Sélectionner votre projet **IrcChat**
3. Consulter :
   - **Quality Gate** : Status global
   - **Bugs** : Erreurs de code
   - **Vulnerabilities** : Failles de sécurité
   - **Code Smells** : Mauvaises pratiques
   - **Coverage** : Couverture de tests
   - **Duplications** : Code dupliqué

### Dans les Pull Requests

SonarCloud commentera automatiquement vos PRs avec :
- ✅ Status du Quality Gate
- 📊 Nouvelles issues détectées
- 🔍 Changements de couverture
- 📈 Évolution de la dette technique

### Badges

Ajouter les badges SonarCloud dans votre `README.md` :

```markdown
[![Quality Gate Status](https://sonarcloud.io/api/project_badges/measure?project=VOTRE_PROJECT_KEY&metric=alert_status)](https://sonarcloud.io/summary/new_code?id=VOTRE_PROJECT_KEY)
[![Coverage](https://sonarcloud.io/api/project_badges/measure?project=VOTRE_PROJECT_KEY&metric=coverage)](https://sonarcloud.io/summary/new_code?id=VOTRE_PROJECT_KEY)
[![Bugs](https://sonarcloud.io/api/project_badges/measure?project=VOTRE_PROJECT_KEY&metric=bugs)](https://sonarcloud.io/summary/new_code?id=VOTRE_PROJECT_KEY)
[![Security Rating](https://sonarcloud.io/api/project_badges/measure?project=VOTRE_PROJECT_KEY&metric=security_rating)](https://sonarcloud.io/summary/new_code?id=VOTRE_PROJECT_KEY)
[![Maintainability Rating](https://sonarcloud.io/api/project_badges/measure?project=VOTRE_PROJECT_KEY&metric=sqale_rating)](https://sonarcloud.io/summary/new_code?id=VOTRE_PROJECT_KEY)
```

Remplacez `VOTRE_PROJECT_KEY` par la valeur de votre variable `SONAR_PROJECT_KEY`.

## Quality Gates

### Configuration par défaut

SonarCloud applique un Quality Gate avec les critères suivants :
- ✅ Coverage ≥ 80%
- ✅ Duplications ≤ 3%
- ✅ Security Hotspots Reviewed = 100%
- ✅ Maintainability Rating = A
- ✅ Reliability Rating = A
- ✅ Security Rating = A

### Personnaliser le Quality Gate

1. Sur SonarCloud : **Quality Gates**
2. Créer un nouveau Quality Gate ou modifier celui par défaut
3. Ajouter/modifier les conditions selon vos besoins

## Exclusions de code

### Exclure des fichiers spécifiques

Dans `sonar-project.properties` :
```properties
sonar.exclusions=**/MaClasseAExclure.cs
```

### Exclure des lignes de code

Avec des commentaires dans le code :
```csharp
#pragma warning disable S1234 // SonarRule
public void MyMethod()
{
    // Code à exclure
}
#pragma warning restore S1234
```

Ou avec des attributs :
```csharp
[System.Diagnostics.CodeAnalysis.SuppressMessage("SonarRule", "S1234")]
public void MyMethod()
{
    // Code à exclure
}
```

## Analyse locale

### Installation

```powershell
dotnet tool install --global dotnet-sonarscanner
```

### Lancer l'analyse localement

```powershell
# Définir les variables (remplacer par vos vraies valeurs)
$SONAR_TOKEN = "votre_token"
$SONAR_PROJECT_KEY = "votre_project_key"
$SONAR_ORGANIZATION = "votre_organization"

# Début de l'analyse
dotnet sonarscanner begin `
  /k:"$SONAR_PROJECT_KEY" `
  /o:"$SONAR_ORGANIZATION" `
  /d:sonar.token="$SONAR_TOKEN" `
  /d:sonar.host.url="https://sonarcloud.io"

# Build
dotnet build --configuration Release

# Tests avec couverture
dotnet test `
  --configuration Release `
  --collect:"XPlat Code Coverage" `
  -- DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Format=opencover

# Fin de l'analyse
dotnet sonarscanner end /d:sonar.token="$SONAR_TOKEN"
```

## Résolution des problèmes courants

### Erreur "Quality Gate failed"

1. Consulter les détails sur SonarCloud
2. Corriger les issues identifiées
3. Re-push le code

### Erreur de couverture insuffisante

1. Ajouter plus de tests unitaires
2. Vérifier les exclusions dans `sonar-project.properties`
3. S'assurer que les rapports de couverture sont générés correctement

### Variables non définies

Si vous voyez des erreurs comme `SONAR_PROJECT_KEY not found` :
1. Vérifier que les variables sont bien définies dans GitHub
2. Vérifier l'orthographe exacte des noms de variables
3. Vérifier que vous êtes bien dans le bon repository

### Timeout de l'analyse

Augmenter le timeout dans le workflow :
```yaml
- name: 📊 SonarCloud Quality Gate check
  timeout-minutes: 10  # Augmenter si nécessaire
```

## Métriques importantes

### Coverage (Couverture)
- **Objectif** : ≥ 80%
- Pourcentage de code couvert par les tests

### Maintainability (Maintenabilité)
- **Objectif** : Rating A
- Temps estimé pour corriger les code smells

### Reliability (Fiabilité)
- **Objectif** : Rating A
- Nombre de bugs détectés

### Security (Sécurité)
- **Objectif** : Rating A
- Vulnérabilités et hotspots de sécurité

### Duplication
- **Objectif** : ≤ 3%
- Pourcentage de code dupliqué

## Checklist d'intégration

- [ ] Compte SonarCloud créé
- [ ] Projet IrcChat ajouté sur SonarCloud
- [ ] Secret `SONAR_TOKEN` configuré dans GitHub
- [ ] Variable `SONAR_PROJECT_KEY` configurée dans GitHub
- [ ] Variable `SONAR_ORGANIZATION` configurée dans GitHub
- [ ] Premier PR créé pour tester l'intégration
- [ ] Quality Gate passe avec succès
- [ ] Badges ajoutés au README.md

## Commandes utiles

```powershell
# Vérifier la configuration SonarCloud
dotnet sonarscanner --help

# Nettoyer les fichiers SonarCloud
Remove-Item -Recurse -Force .sonarqube

# Régénérer les rapports de couverture
dotnet test --collect:"XPlat Code Coverage" -- DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Format=opencover
```

## Liens utiles

- [Documentation SonarCloud](https://docs.sonarcloud.io/)
- [Rules pour C#](https://rules.sonarsource.com/csharp)
- [SonarScanner for .NET](https://docs.sonarcloud.io/advanced-setup/ci-based-analysis/sonarscanner-for-net/)
- [Quality Gates](https://docs.sonarcloud.io/improving/quality-gates/)
- [GitHub Variables](https://docs.github.com/en/actions/learn-github-actions/variables)

## Support

En cas de problème :
1. Consulter les [FAQ SonarCloud](https://community.sonarsource.com/c/help/sc/9)
2. Vérifier les logs dans GitHub Actions
3. Contacter le support SonarCloud