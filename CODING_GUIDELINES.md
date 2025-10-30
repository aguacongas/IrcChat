# Directives de codage - IrcChat

## Conventions C# à respecter

### Constructeurs primaires (C# 12+)
✅ **Toujours utiliser les constructeurs primaires** pour les classes avec injection de dépendances :

```csharp
// ✅ BON
public class MyService(ILogger<MyService> logger, IConfiguration config)
{
    private readonly string _apiKey = config["ApiKey"];
    
    public void DoSomething()
    {
        logger.LogInformation("Doing something");
    }
}

// ❌ ÉVITER
public class MyService
{
    private readonly ILogger<MyService> _logger;
    private readonly IConfiguration _config;
    
    public MyService(ILogger<MyService> logger, IConfiguration config)
    {
        _logger = logger;
        _config = config;
    }
}
```

### API Fluente et extension methods
✅ **Utiliser la notation fluente** pour la configuration et l'enregistrement de services :

```csharp
// ✅ BON - Fluent API
services
    .AddDatabaseServices(configuration)
    .AddApplicationServices(configuration)
    .AddJwtAuthentication(configuration);

app
    .ConfigurePipeline()
    .MapApiEndpoints();

// ❌ ÉVITER - Appels séparés
services.AddDatabaseServices(configuration);
services.AddApplicationServices(configuration);
services.AddJwtAuthentication(configuration);
```

### Entity Framework
✅ **Utiliser la configuration fluente** dans `OnModelCreating` :

```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.Entity<Message>(entity =>
    {
        entity.HasKey(e => e.Id);
        entity.HasIndex(e => e.Channel);
        entity.HasIndex(e => e.Timestamp);
    });
}
```

### LINQ et requêtes
✅ **Préférer la syntaxe de méthode fluente** :

```csharp
// ✅ BON
var users = await db.ConnectedUsers
    .Where(u => u.Channel == channel)
    .OrderBy(u => u.Username)
    .Select(u => new User { ... })
    .ToListAsync();

// ❌ ÉVITER - Query syntax
var users = await (from u in db.ConnectedUsers
                   where u.Channel == channel
                   orderby u.Username
                   select new User { ... }).ToListAsync();
```

## Architecture

- **Clean separation** : API, Client, Shared projects
- **Extension methods** pour organiser la configuration
- **Background services** pour les tâches périodiques
- **SignalR** pour la communication temps réel
- **EF Core** avec PostgreSQL

## Patterns utilisés

- Repository pattern (via DbContext)
- Dependency Injection
- Options pattern pour la configuration
- Background services pour les tâches planifiées

## Technologies

- .NET 9.0
- Blazor WebAssembly
- SignalR
- Entity Framework Core
- PostgreSQL
- JWT Authentication
- OAuth 2.0 (Google, Facebook, Microsoft)