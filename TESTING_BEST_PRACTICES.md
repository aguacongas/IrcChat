# Bonnes pratiques de test - IrcChat

## ⚠️ Règle critique : Cache Entity Framework Core

### Le problème du cache de premier niveau

Entity Framework Core maintient un **cache de premier niveau** (Identity Map) :
- Les entités chargées sont trackées dans le contexte
- `FindAsync()` retourne d'abord depuis le cache
- Les changements en BDD ne sont pas visibles dans le même contexte

### Solution : Toujours créer un nouveau scope pour les vérifications

**❌ MAUVAIS** - Réutilise le même contexte
```csharp
[Fact]
public async Task Test_BadExample()
{
    using var scope = _factory.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<ChatDbContext>();

    // Arrange & Act
    await _client.PostAsync("/api/endpoint", data);

    // Assert - ❌ PROBLÈME: Cache EF Core
    var result = await db.SomeTable.FindAsync(id);
    result.Should().BeNull(); // Peut échouer à cause du cache
}
```

**✅ BON** - Nouveau scope et contexte
```csharp
[Fact]
public async Task Test_GoodExample()
{
    using var scope = _factory.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<ChatDbContext>();

    // Arrange & Act
    await _client.PostAsync("/api/endpoint", data);

    // Assert - ✅ Nouveau contexte, pas de cache
    using var verifyScope = _factory.Services.CreateScope();
    using var verifyContext = verifyScope.ServiceProvider.GetRequiredService<ChatDbContext>();
    var result = await verifyContext.SomeTable.FindAsync(id);
    result.Should().BeNull(); // Fiable
}
```

---

## 📋 Pattern de test complet

```csharp
[Fact]
public async Task MethodName_Scenario_ExpectedBehavior()
{
    // ===== ARRANGE =====
    using var scope = _factory.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<ChatDbContext>();

    // Créer les données de test
    var testData = new Entity { ... };
    db.Entities.Add(testData);
    await db.SaveChangesAsync();

    // Configurer l'authentification si nécessaire
    var token = GenerateToken(user);
    _client.DefaultRequestHeaders.Authorization = 
        new AuthenticationHeaderValue("Bearer", token);

    // ===== ACT =====
    var response = await _client.PostAsJsonAsync("/api/endpoint", request);

    // ===== ASSERT =====
    // 1. Vérifier le status HTTP
    response.StatusCode.Should().Be(HttpStatusCode.OK);

    // 2. Vérifier la réponse
    var result = await response.Content.ReadFromJsonAsync<ResponseType>();
    result.Should().NotBeNull();
    result!.Property.Should().Be(expectedValue);

    // 3. Vérifier les changements en BDD (NOUVEAU SCOPE!)
    using var verifyScope = _factory.Services.CreateScope();
    using var verifyContext = verifyScope.ServiceProvider.GetRequiredService<ChatDbContext>();
    
    var savedEntity = await verifyContext.Entities.FindAsync(testData.Id);
    savedEntity.Should().NotBeNull();
    savedEntity!.Property.Should().Be(expectedValue);
}
```

---

## 🎯 Checklist de test

### Tests d'endpoints API
- [ ] **Happy path** - Fonctionnement normal
- [ ] **Validation** - Données invalides → BadRequest
- [ ] **Authentication** - Sans token → Unauthorized
- [ ] **Authorization** - Utilisateur non autorisé → Forbidden
- [ ] **Not Found** - Ressource inexistante → NotFound
- [ ] **Duplicate** - Création de doublon → BadRequest
- [ ] **Edge cases** - Valeurs limites, cas spéciaux

### Tests de services
- [ ] **Logique métier** - Tous les chemins d'exécution
- [ ] **Exceptions** - Gestion des erreurs
- [ ] **Dépendances** - Mocks et interactions
- [ ] **États** - Changements d'état corrects

### Tests SignalR Hub
- [ ] **Connexion/Déconnexion** - OnConnected, OnDisconnected
- [ ] **Envoi de messages** - Broadcast, groupes
- [ ] **Gestion des groupes** - Join, Leave
- [ ] **Clients** - Caller, All, Group, Client

---

## 🔧 Patterns utiles

### 1. Génération de token JWT

```csharp
private static string GenerateToken(ReservedUsername user)
{
    var key = new SymmetricSecurityKey(
        Encoding.UTF8.GetBytes("VotreCleSecrete123456789012345678901234567890"));

    var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

    var claims = new[]
    {
        new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
        new Claim(ClaimTypes.Name, user.Username),
        new Claim(ClaimTypes.Email, user.Email),
        new Claim("provider", user.Provider.ToString())
    };

    var token = new JwtSecurityToken(
        issuer: "IrcChatApi",
        audience: "IrcChatClient",
        claims: claims,
        expires: DateTime.UtcNow.AddHours(1),
        signingCredentials: credentials
    );

    return new JwtSecurityTokenHandler().WriteToken(token);
}
```

### 2. Builder pattern pour données de test

```csharp
public static class TestDataBuilder
{
    public static ReservedUsername CreateUser(
        string username = "testuser",
        bool isAdmin = false)
    {
        return new ReservedUsername
        {
            Id = Guid.NewGuid(),
            Username = username,
            Provider = ExternalAuthProvider.Google,
            ExternalUserId = Guid.NewGuid().ToString(),
            Email = $"{username}@test.com",
            CreatedAt = DateTime.UtcNow,
            LastLoginAt = DateTime.UtcNow,
            IsAdmin = isAdmin
        };
    }
}
```

### 3. Tests SignalR avec mocks

```csharp
private readonly Mock<IHubCallerClients> _clientsMock;
private readonly Mock<IClientProxy> _callerMock;
private readonly Mock<HubCallerContext> _contextMock;
private readonly Mock<IGroupManager> _groupManagerMock;

public MyHubTests()
{
    _clientsMock = new Mock<IHubCallerClients>();
    _callerMock = new Mock<IClientProxy>();
    _contextMock = new Mock<HubCallerContext>();
    _groupManagerMock = new Mock<IGroupManager>();

    _clientsMock.Setup(c => c.Caller).Returns(_callerMock.Object);
    _contextMock.Setup(c => c.ConnectionId).Returns("test-conn-id");

    _hub = new MyHub(db, logger)
    {
        Clients = _clientsMock.Object,
        Context = _contextMock.Object,
        Groups = _groupManagerMock.Object
    };
}
```

## 🌐 Mock de HttpClient avec MockHttpMessageHandler - Le guide complet

### ⚠️ Erreur critique : Ne PAS recréer la requête

**Le problème :** `GetMatchCount()` nécessite la **même instance** de `MockedRequest` que celle retournée par `When()`.

### ❌ MAUVAIS - Recrée la requête

```csharp
// Setup
_mockHttp.When(HttpMethod.Get, "*/api/messages/general")
    .Respond(HttpStatusCode.OK, JsonContent.Create(messages));

// Verify - ❌ ERREUR: Crée une NOUVELLE requête, count sera toujours 0
_mockHttp.GetMatchCount(_mockHttp.When(HttpMethod.Get, "*/api/messages/general"))
    .Should().BeGreaterThanOrEqualTo(1);
```

### ✅ BON - Réutilise la même instance

```csharp
// Setup - 💾 SAUVEGARDER l'instance
var request = _mockHttp.When(HttpMethod.Get, "*/api/messages/general")
    .Respond(HttpStatusCode.OK, JsonContent.Create(messages));

// Verify - ✅ Utilise la MÊME instance
_mockHttp.GetMatchCount(request)
    .Should().BeGreaterThanOrEqualTo(1);
```

### 📋 Pattern complet pour tests frontend

```csharp
[Fact]
public async Task Component_ShouldCallApi_WhenLoaded()
{
    // Arrange
    var messages = new List<MessageDto> { /* ... */ };
    
    // 💾 IMPORTANT: Sauvegarder l'instance retournée par When()
    var getMessagesRequest = _mockHttp
        .When(HttpMethod.Get, "*/api/messages/general")
        .Respond(HttpStatusCode.OK, JsonContent.Create(messages));

    // Act
    var cut = RenderComponent<ChatComponent>(parameters => parameters
        .Add(p => p.ChannelId, "general"));
    
    cut.WaitForState(() => !cut.Markup.Contains("Chargement"), TimeSpan.FromSeconds(2));

    // Assert - ✅ Vérifier avec la même instance
    _mockHttp.GetMatchCount(getMessagesRequest)
        .Should().Be(1, "l'API devrait être appelée une fois au chargement");
}
```

### 🎯 Multiples endpoints

```csharp
[Fact]
public async Task Component_ShouldCallMultipleEndpoints()
{
    // Setup - 💾 Sauvegarder TOUTES les instances
    var getUserRequest = _mockHttp
        .When(HttpMethod.Get, "*/api/users/me")
        .Respond(HttpStatusCode.OK, JsonContent.Create(user));
    
    var getChannelsRequest = _mockHttp
        .When(HttpMethod.Get, "*/api/channels")
        .Respond(HttpStatusCode.OK, JsonContent.Create(channels));
    
    var postMessageRequest = _mockHttp
        .When(HttpMethod.Post, "*/api/messages")
        .Respond(HttpStatusCode.Created);

    // Act
    var cut = RenderComponent<MyComponent>();
    await cut.InvokeAsync(() => cut.Find("button.send").Click());

    // Assert - ✅ Vérifier chaque requête individuellement
    _mockHttp.GetMatchCount(getUserRequest).Should().Be(1);
    _mockHttp.GetMatchCount(getChannelsRequest).Should().Be(1);
    _mockHttp.GetMatchCount(postMessageRequest).Should().Be(1);
}
```

### 💡 Aide-mémoire

- `When()` retourne un `MockedRequest` → **TOUJOURS le sauvegarder dans une variable**
- `GetMatchCount()` a besoin de la **même instance** de `MockedRequest`
- **JAMAIS** appeler `When()` deux fois avec les mêmes paramètres
- Une requête = une variable = un `When()` = un `GetMatchCount()`

---

## 🎓 Mock de IJSRuntime - Le guide complet

### Pourquoi c'est piégeux ?

`InvokeVoidAsync` est une **méthode d'extension**, elle ne peut **jamais** être mockée avec Moq. Il faut mocker la méthode sous-jacente : `InvokeAsync<IJSVoidResult>`.

### ✅ Méthodes qui retournent une valeur

```csharp
_jsRuntimeMock
    .Setup(x => x.InvokeAsync<string?>(
        "localStorageHelper.getItem",
        It.Is<object[]>(o => o.Length == 1 && (string)o[0] == "my-key")))
    .ReturnsAsync("my-value");
```

### ✅ Opérations void (setItem, removeItem, clear)

```csharp
_jsRuntimeMock
    .Setup(x => x.InvokeAsync<IJSVoidResult>(
        "localStorageHelper.setItem",
        It.IsAny<object[]>()))
    .ReturnsAsync((IJSVoidResult)null!);

_jsRuntimeMock
    .Setup(x => x.InvokeAsync<IJSVoidResult>(
        "localStorageHelper.removeItem",
        It.IsAny<object[]>()))
    .ReturnsAsync((IJSVoidResult)null!);
```

### ✅ Vérifier les appels

```csharp
_jsRuntimeMock.Verify(
    x => x.InvokeAsync<IJSVoidResult>(
        "localStorageHelper.setItem",
        It.Is<object[]>(o => 
            o.Length == 2 && 
            (string)o[0] == "my-key" && 
            (string)o[1] == "my-value")),
    Times.Once);
```

### ❌ Erreurs à éviter

```csharp
// ❌ Impossible: InvokeVoidAsync est une extension
_jsRuntimeMock
    .Setup(x => x.InvokeVoidAsync("method", args))
    .Returns(ValueTask.CompletedTask);

// ❌ InvalidCastException: Mauvais type
_jsRuntimeMock
    .Setup(x => x.InvokeAsync<IJSVoidResult>("method", args))
    .ReturnsAsync(new object());

// ❌ InvalidCastException: Mauvais type générique
_jsRuntimeMock
    .Setup(x => x.InvokeAsync<IJSVoidResult>("method", args))
    .Returns(ValueTask.FromResult<object>(null!));
```

### 💡 Aide-mémoire

- `InvokeAsync<T>` retourne `ValueTask<T>`
- Pour void → `T` = `IJSVoidResult`
- Donc → `Returns(ValueTask.FromResult<IJSVoidResult>(null!))`
- **JAMAIS** mocker `InvokeVoidAsync` (c'est une extension)
- **TOUJOURS** mocker `InvokeAsync<IJSVoidResult>` à la place

---

## 🚫 Pièges à éviter

### ❌ Oublier SaveChangesAsync

```csharp
// ❌ MAUVAIS
db.Entities.Add(entity);
// L'entité n'est pas en BDD!

// ✅ BON
db.Entities.Add(entity);
await db.SaveChangesAsync();
```

### ❌ Réutiliser le même HttpClient avec auth

```csharp
// ❌ MAUVAIS - Le token reste pour tous les tests
_client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

// ✅ BON - Créer un nouveau client ou nettoyer
var client = _factory.CreateClient();
client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
```

### ❌ Ignorer les codes de statut

```csharp
// ❌ MAUVAIS
var result = await response.Content.ReadFromJsonAsync<Response>();

// ✅ BON
response.StatusCode.Should().Be(HttpStatusCode.OK);
var result = await response.Content.ReadFromJsonAsync<Response>();
```

### ❌ Ne pas forcer un re-render avant d'interagir avec les éléments (bUnit)

```csharp
// ❌ MAUVAIS - Peut causer UnknownEventHandlerIdException
var cut = RenderComponent<AdminPanel>(parameters => parameters
    .Add(p => p.CurrentUserId, currentUserId));
cut.WaitForState(() => !cut.Markup.Contains("Chargement"), TimeSpan.FromSeconds(2));

// Act
var promoteButton = cut.Find(".btn-action.promote");
await cut.InvokeAsync(() => promoteButton.Click());

// ✅ BON - Forcer un re-render après WaitForState
var cut = RenderComponent<AdminPanel>(parameters => parameters
    .Add(p => p.CurrentUserId, currentUserId));
cut.WaitForState(() => !cut.Markup.Contains("Chargement"), TimeSpan.FromSeconds(2));
cut.Render(); // 👈 Force un re-render pour synchroniser le DOM

// Act
var promoteButton = cut.Find(".btn-action.promote");
await cut.InvokeAsync(() => promoteButton.Click());
```

**Pourquoi ?**
- `WaitForState()` attend un changement de DOM mais ne garantit pas que les event handlers sont à jour
- Entre `WaitForState()` et `Find()`, le composant peut avoir changé d'état
- `cut.Render()` force une synchronisation complète du render tree
- Cela garantit que les IDs des event handlers sont corrects

**Erreur typique :**
```
Bunit.Rendering.UnknownEventHandlerIdException: 
There is no event handler with ID '1' associated with the 'onclick' 
event in the current render tree.
```

**Alternatives :**
```csharp
// Alternative 1: Re-trouver l'élément après chaque render
cut.WaitForState(() => !cut.Markup.Contains("Chargement"), TimeSpan.FromSeconds(2));
await cut.InvokeAsync(() => cut.Find(".btn-action.promote").Click());

// Alternative 2: Wrapper Find + Click dans InvokeAsync (recommandé)
cut.WaitForState(() => !cut.Markup.Contains("Chargement"), TimeSpan.FromSeconds(2));
await cut.InvokeAsync(() => cut.Find(".btn-action.promote").Click());
```

### ❌ Utiliser cut.Dispose() pour tester IAsyncDisposable

```csharp
// ❌ MAUVAIS - cut.Dispose() n'appelle PAS DisposeAsync()
[Fact]
public async Task Component_WhenDisposed_ShouldDisposeResources()
{
    var cut = RenderComponent<MyComponent>();
    
    // Act
    cut.Dispose(); // ⚠️ N'appelle pas IAsyncDisposable.DisposeAsync()
    
    // Assert
    mockResource.Verify(x => x.DisposeAsync(), Times.Once); // ❌ Échouera
}

// ✅ BON - Utiliser cut.Instance.DisposeAsync()
[Fact]
public async Task Component_WhenDisposed_ShouldDisposeResources()
{
    var mockModule = new Mock<IJSObjectReference>();
    _jsRuntimeMock
        .Setup(x => x.InvokeAsync<IJSObjectReference>("import", It.IsAny<object[]>()))
        .ReturnsAsync(mockModule.Object);
    
    mockModule
        .Setup(x => x.DisposeAsync())
        .Returns(ValueTask.CompletedTask);
    
    var cut = RenderComponent<MyComponent>();
    await Task.Delay(100); // Attendre le chargement initial
    
    // Act - ✅ Appelle bien IAsyncDisposable.DisposeAsync()
    await cut.Instance.DisposeAsync();
    await Task.Delay(100);
    
    // Assert
    mockModule.Verify(x => x.DisposeAsync(), Times.Once); // ✅ Passe
}
```

**Pourquoi ?**
- `cut.Dispose()` appelle `IDisposable.Dispose()`, pas `IAsyncDisposable.DisposeAsync()`
- Pour tester la méthode `DisposeAsync()` d'un composant, il faut l'appeler explicitement via `cut.Instance.DisposeAsync()`
- C'est particulièrement important pour les composants qui utilisent des modules JS ou d'autres ressources asynchrones

**Pattern complet pour IAsyncDisposable :**
```csharp
[Fact]
public async Task MessageList_WhenDisposed_ShouldDisposeModule()
{
    // Arrange
    var mockModule = new Mock<IJSObjectReference>();
    
    _jsRuntimeMock
        .Setup(x => x.InvokeAsync<IJSObjectReference>("import", It.IsAny<object[]>()))
        .ReturnsAsync(mockModule.Object);
    
    mockModule
        .Setup(x => x.DisposeAsync())
        .Returns(ValueTask.CompletedTask);
    
    var messages = new List<Message>();
    
    var cut = RenderComponent<MessageList>(parameters => parameters
        .Add(p => p.Messages, messages)
        .Add(p => p.CurrentUsername, "user1"));
    
    await Task.Delay(100); // Attendre que le module soit chargé
    
    // Act - ✅ Appelle IAsyncDisposable.DisposeAsync()
    await cut.Instance.DisposeAsync();
    await Task.Delay(100);
    
    // Assert
    mockModule.Verify(x => x.DisposeAsync(), Times.Once);
}
```

---

## 📊 Structure des tests

```
tests/
├── IrcChat.Api.Tests/
│   ├── Authorization/         # 🆕 Tests des Authorization Handlers
│   │   └── *HandlerTests.cs
│   ├── Integration/           # Tests d'intégration (endpoints)
│   │   └── *EndpointsTests.cs
│   ├── Services/              # Tests unitaires (services)
│   │   └── *ServiceTests.cs
│   ├── Hubs/                  # Tests SignalR
│   │   └── *HubTests.cs
│   └── Helpers/               # Utilitaires de test
│       ├── TestDataBuilder.cs
│       ├── HttpClientExtensions.cs
│       └── TestDbContextFactory.cs
├── IrcChat.Client.Tests/
│   ├── Components/            # Tests des composants Blazor
│   │   └── *Tests.cs
│   ├── Pages/                 # Tests des pages
│   │   └── *Tests.cs
│   ├── Services/              # Tests des services client
│   │   └── *ServiceTests.cs
│   └── Helpers/               # Utilitaires de test
│       └── BunitTestContext.cs
```

---

## 📚 Ressources

- [xUnit Documentation](https://xunit.net/)
- [Moq Quickstart](https://github.com/moq/moq4/wiki/Quickstart)
- [EF Core Testing](https://learn.microsoft.com/en-us/ef/core/testing/)
- [SignalR Testing](https://learn.microsoft.com/en-us/aspnet/core/signalr/testing)
- [Authorization Testing](https://learn.microsoft.com/en-us/aspnet/core/security/authorization/resourcebased)
- [bUnit Documentation](https://bunit.dev/)
- [JSInterop Testing](https://bunit.dev/docs/test-doubles/emulating-ijsruntime)

---

## ✅ Exemples complets

### Backend (API)
- `ChannelModificationHandlerTests.cs` - Tests d'Authorization Handler
- `ChannelDeleteEndpointsTests.cs` - Pattern complet avec vérification BDD
- `AdminManagementEndpointsTests.cs` - Tests avec autorisation
- `ChatHubTests.cs` - Tests SignalR avec mocks
- `OAuthEndpointsTests.cs` - Tests d'authentification

### Frontend (Client)
- `ChannelMuteButtonTests.cs` - Tests de composant avec HTTP mock
- `ChatTests.cs` - Tests de page complexe avec SignalR
- `UnifiedAuthServiceTests.cs` - Tests avec JSRuntime mock
- `OAuthClientServiceTests.cs` - Tests OAuth avec PKCE

---

## 🎯 Objectifs de couverture

- **Endpoints API** : ≥ 85%
- **Services** : ≥ 80%
- **Hubs SignalR** : ≥ 75%
- **Extensions** : ≥ 70%
- **Composants Blazor** : ≥ 70%
- **Global** : ≥ 80%

---

## 🔍 Vérifier la couverture localement

```powershell
# Lancer les tests avec couverture
dotnet test --collect:"XPlat Code Coverage" -- DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Format=opencover

# Générer un rapport HTML
dotnet tool install --global dotnet-reportgenerator-globaltool
reportgenerator -reports:"**/coverage.opencover.xml" -targetdir:"CoverageReport" -reporttypes:Html

# Ouvrir le rapport
Start-Process "CoverageReport/index.html"
```

---

## 📚 Ressources

- [xUnit Documentation](https://xunit.net/)
- [FluentAssertions](https://fluentassertions.com/)
- [Moq Quickstart](https://github.com/moq/moq4/wiki/Quickstart)
- [EF Core Testing](https://learn.microsoft.com/en-us/ef/core/testing/)
- [SignalR Testing](https://learn.microsoft.com/en-us/aspnet/core/signalr/testing)
- [bUnit Documentation](https://bunit.dev/)
- [JSInterop Testing](https://bunit.dev/docs/test-doubles/emulating-ijsruntime)

---

## ✅ Exemples complets

### Backend (API)
- `ChannelDeleteEndpointsTests.cs` - Pattern complet avec vérification BDD
- `AdminManagementEndpointsTests.cs` - Tests avec autorisation
- `ChatHubTests.cs` - Tests SignalR avec mocks
- `OAuthEndpointsTests.cs` - Tests d'authentification

### Frontend (Client)
- `ChannelMuteButtonTests.cs` - Tests de composant avec HTTP mock
- `ChatTests.cs` - Tests de page complexe avec SignalR
- `UnifiedAuthServiceTests.cs` - Tests avec JSRuntime mock
- `OAuthClientServiceTests.cs` - Tests OAuth avec PKCE