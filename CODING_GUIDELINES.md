# Directives de codage - IrcChat

## Conventions C# à respecter

### Accolades obligatoires
✅ **Toujours utiliser des accolades** même pour les blocs d'une seule ligne :
```csharp
// ✅ BON
if (condition)
{
    DoSomething();
}

// ❌ ÉVITER
if (condition)
    DoSomething();

// ✅ BON
for (int i = 0; i < 10; i++)
{
    Process(i);
}

// ❌ ÉVITER
for (int i = 0; i < 10; i++)
    Process(i);
```

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

### Initialiseurs de collection
✅ **Utiliser les initialiseurs de collection** plutôt que Add() :
```csharp
// ✅ BON
var list = new List<string>
{
    "item1",
    "item2",
    "item3"
};

var dict = new Dictionary<string, int>
{
    ["key1"] = 1,
    ["key2"] = 2
};

// ❌ ÉVITER
var list = new List<string>();
list.Add("item1");
list.Add("item2");
list.Add("item3");

var dict = new Dictionary<string, int>();
dict.Add("key1", 1);
dict.Add("key2", 2);

// ✅ BON - Collection expressions (C# 12)
List<string> list = ["item1", "item2", "item3"];

// ✅ BON - Spread operator (C# 12)
var combined = [..list1, ..list2];
```

### Expression-bodied members
✅ **Utiliser les expression-bodied members** pour les méthodes simples :
```csharp
// ✅ BON - Expression body pour méthodes simples
public string GetFullName() => $"{FirstName} {LastName}";

public int Calculate(int x, int y) => x + y;

public bool IsValid() => Age >= 18 && !string.IsNullOrEmpty(Name);

// ✅ BON - Properties
public string FullName => $"{FirstName} {LastName}";

public bool IsAdult => Age >= 18;

// ✅ BON - Accessors
private string _name;
public string Name
{
    get => _name;
    set => _name = value?.Trim() ?? string.Empty;
}

// ❌ ÉVITER - Bloc complet pour une expression simple
public string GetFullName()
{
    return $"{FirstName} {LastName}";
}

public int Calculate(int x, int y)
{
    return x + y;
}

// ⚠️ ACCEPTABLE - Méthodes complexes avec bloc
public async Task<User> GetUserAsync(int id)
{
    var user = await _db.Users.FindAsync(id);
    if (user == null)
    {
        throw new NotFoundException();
    }
    return user;
}

// ⚠️ ACCEPTABLE - Constructeurs (toujours avec bloc)
public MyService(ILogger logger, IConfiguration config)
{
    _logger = logger;
    _config = config;
}
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

