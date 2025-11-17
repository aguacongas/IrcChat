# Contributing to IrcChat

Merci de votre intérêt pour contribuer à IrcChat ! 🎉

## 🤖 Particularité de ce projet

**Ce projet est unique** : le code est principalement généré par **Claude (Anthropic)**, une IA développée par Anthropic. Le mainteneur (humain) effectue du code review et demande des corrections/améliorations à Claude.

### Comment ça fonctionne ?

1. **Spécification** → L'humain décrit la fonctionnalité à Claude
2. **Génération** → Claude génère le code et les tests
3. **Review** → L'humain vérifie la conformité et la qualité
4. **Validation** → GitHub Actions et SonarCloud valident automatiquement
5. **Merge** → Après validation du Quality Gate

## 🤝 Comment contribuer ?

### Option 3 : Contribuer du code (Avancé)

Si vous souhaitez contribuer du code directement :

#### Prérequis

- ✅ Lire et comprendre les [Directives de codage](CODING_GUIDELINES.md)
- ✅ Lire la [Politique de tests](TEST_POLICY.md)
- ✅ Lire les [Bonnes pratiques de test](TESTING_BEST_PRACTICES.md)
- ✅ Fork du repository
- ✅ .NET 10 SDK installé
- ✅ PostgreSQL 16+ installé

#### Workflow de contribution

1. **Fork & Clone**
   ```bash
   git clone https://github.com/VOTRE_USERNAME/IrcChat.git
   cd IrcChat
   git checkout -b feature/ma-fonctionnalite
   ```

2. **Configuration**
   ```bash
   # Restaurer les packages
   dotnet restore
   
   # Configurer la base de données
   dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Database=ircchat_dev;Username=postgres;Password=postgres"
   
   # Appliquer les migrations
   cd src/IrcChat.Api
   dotnet ef database update
   ```

[... le reste du fichier reste identique ...]

### Technologies
- [.NET 10 Documentation](https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-10)
- [Blazor Documentation](https://learn.microsoft.com/en-us/aspnet/core/blazor/)
- [SignalR Documentation](https://learn.microsoft.com/en-us/aspnet/core/signalr/)
- [Entity Framework Core](https://learn.microsoft.com/en-us/ef/core/)
- [PostgreSQL Documentation](https://www.postgresql.org/docs/)

[... suite du fichier inchangée ...]