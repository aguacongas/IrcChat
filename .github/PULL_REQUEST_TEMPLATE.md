## 📝 Description

<!-- Décrire brièvement les changements apportés -->

## 🤖 Génération

- [ ] Code généré par Claude (Anthropic)
- [ ] Code généré par opencode (assistant)
- [ ] Code review effectué
- [ ] Tests générés automatiquement

## 🎯 Type de changement

- [ ] 🐛 Bug fix (correction non-breaking)
- [ ] ✨ Nouvelle fonctionnalité (changement non-breaking)
- [ ] 💥 Breaking change (correction ou fonctionnalité causant un breaking change)
- [ ] 📝 Documentation
- [ ] 🎨 Style/Refactoring (pas de changement fonctionnel)
- [ ] ⚡ Performance
- [ ] 🧪 Tests
- [ ] 🔧 Configuration/Build

## 📋 Checklist

### Code
- [ ] Le code suit les [directives de codage](../CODING_GUIDELINES.md)
- [ ] Constructeurs primaires utilisés (si applicable)
- [ ] Accolades obligatoires pour tous les blocs
- [ ] Expression-bodied members pour méthodes simples
- [ ] API fluente pour configuration
- [ ] Pas de code mort ou commenté

### Tests
- [ ] Tests unitaires créés/mis à jour
- [ ] Tests d'intégration créés/mis à jour (si endpoint API)
- [ ] Tests de composants créés/mis à jour (si UI Blazor)
- [ ] Tous les tests passent localement
- [ ] Couverture de code ≥ 80%
- [ ] Respect des [bonnes pratiques de test](../TESTING_BEST_PRACTICES.md)

### Documentation
- [ ] Documentation mise à jour (si nécessaire)
- [ ] Commentaires XML pour API publiques
- [ ] README mis à jour (si nouvelle fonctionnalité majeure)
- [ ] CHANGELOG mis à jour (si applicable)

### Qualité
- [ ] `dotnet format` exécuté
- [ ] Pas de warnings de compilation
- [ ] SonarCloud Quality Gate passera (vérifié localement si possible)
- [ ] Pas de vulnérabilités de sécurité introduites

## 🧪 Tests effectués

### Tests automatiques
```bash
# Commandes exécutées
dotnet test
dotnet test --collect:"XPlat Code Coverage"
```

### Tests manuels
<!-- Décrire les tests manuels effectués, si applicable -->

- [ ] Test 1 : ...
- [ ] Test 2 : ...

## 📊 Couverture de code

<!-- Indiquer la couverture avant/après si pertinent -->

- **Avant** : X%
- **Après** : X%
- **Changement** : +/- X%

## 🔗 Issues liées

<!-- Lier les issues GitHub concernées -->

Fixes #(numéro)
Closes #(numéro)
Related to #(numéro)

## 📸 Screenshots/Vidéos

<!-- Si changement UI, ajouter des captures d'écran ou vidéos -->

### Avant
<!-- Screenshot -->

### Après
<!-- Screenshot -->

## 🚀 Déploiement

- [ ] Aucune migration de base de données requise
- [ ] Migration de base de données requise (détails ci-dessous)
- [ ] Variables d'environnement à ajouter
- [ ] Configuration à mettre à jour

### Migrations
```bash
# Commandes de migration si applicable
dotnet ef migrations add MigrationName
dotnet ef database update
```

### Variables d'environnement
```bash
# Variables à configurer si applicable
VARIABLE_NAME=value
```

## 💡 Notes supplémentaires

<!-- Informations supplémentaires, décisions techniques, points d'attention -->

## 📝 Review checklist (pour le reviewer)

- [ ] Code conforme aux guidelines
- [ ] Tests suffisants et pertinents
- [ ] Pas de régression introduite
- [ ] Performance acceptable
- [ ] Sécurité vérifiée
- [ ] Documentation adéquate
- [ ] Prêt pour merge

---

> 🤖 **Rappel** : Ce code a été généré par Claude. Le review humain se concentre sur la conformité aux spécifications, la qualité globale et les aspects que l'IA pourrait avoir manqués.