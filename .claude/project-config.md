# Configuration Claude - IrcChat

## 🎯 Mode de fonctionnement : Socratic Method

Claude utilise la **méthode socratique** pour ce projet :
- **Poser des questions** avant de générer du code
- **Clarifier** les spécifications vagues
- **Présenter un plan** avant l'implémentation
- **Attendre validation** avant de générer

## 📋 Questions obligatoires avant génération

Avant de générer du code, Claude DOIT poser ces questions si les réponses ne sont pas claires :

### 1. Scope et Architecture
```
- Cette fonctionnalité concerne-t-elle :
  □ Backend (API) uniquement ?
  □ Frontend (Blazor) uniquement ?
  □ Les deux ?
  □ SignalR (temps réel) ?

- Quels fichiers seront impactés ?
  □ Nouveaux fichiers à créer
  □ Fichiers existants à modifier
  □ Migrations de base de données

- Y a-t-il des dépendances avec du code existant ?
  □ Services existants à utiliser
  □ Modèles existants à étendre
  □ Hubs SignalR à modifier
```

### 2. Spécifications fonctionnelles
```
- Scénarios utilisateur concrets :
  □ Qui fait quoi dans quel contexte ?
  □ Quel est le résultat attendu ?
  □ Quels sont les cas d'erreur possibles ?

- Règles métier :
  □ Quelles validations sur les données ?
  □ Quelles contraintes métier ?
  □ Quels calculs ou transformations ?

- Flux de données :
  □ D'où viennent les données ?
  □ Comment sont-elles traitées ?
  □ Où sont-elles stockées/envoyées ?
```

### 3. Authorization et Sécurité
```
- Qui peut exécuter cette action ?
  □ Tous les utilisateurs authentifiés ?
  □ Admins uniquement ?
  □ Owner/Créateur de la ressource ?
  □ Combinaison de rôles/permissions ?

- Validations de sécurité :
  □ Quelles validations sur les inputs ?
  □ Protection contre XSS/SQL injection ?
  □ Rate limiting nécessaire ?
  □ Audit logging requis ?

- Données sensibles :
  □ Y a-t-il des données sensibles ?
  □ Chiffrement nécessaire ?
  □ Masquage dans les logs ?
```

### 4. Tests requis
```
- Scénarios de test prioritaires :
  □ Happy path (fonctionnement normal)
  □ Validation (données invalides)
  □ Authorization (accès non autorisé)
  □ Not Found (ressource inexistante)
  □ Edge cases spécifiques

- Type de tests nécessaires :
  □ Tests unitaires (services, logique)
  □ Tests d'intégration (endpoints API)
  □ Tests de composants (UI Blazor)
  □ Tests SignalR (hubs)

- Niveau de couverture visé :
  □ Standard : ≥ 80%
  □ Critique : 100%
  □ Nouveau code seulement ou aussi existant ?
```

### 5. Contraintes techniques
```
- Performance :
  □ Temps de réponse maximum ?
  □ Volume de données attendu ?
  □ Besoin de pagination ?
  □ Besoin de cache ?

- Scalabilité :
  □ Nombre d'utilisateurs simultanés ?
  □ Fréquence d'utilisation ?
  □ Impact sur les resources (DB, mémoire) ?

- Compatibilité :
  □ Impact sur le code existant ?
  □ Migration de données nécessaire ?
  □ Backward compatibility requise ?
```

### 6. SignalR et Temps Réel (si applicable)
```
- Communication temps réel :
  □ Quels événements doivent être broadcastés ?
  □ À qui (tous, groupe, utilisateur spécifique) ?
  □ Quelle fréquence de mise à jour ?

- Gestion des connexions :
  □ Impact sur ConnectionManager ?
  □ Groupes SignalR à gérer ?
  □ Reconnexion automatique ?
```

## 🔄 Processus de génération en 4 étapes

### Étape 1️⃣ : CLARIFICATION (OBLIGATOIRE)

```
Claude dit :
"Avant de générer le code, j'ai besoin de clarifier quelques points :

[Pose les questions pertinentes parmi celles ci-dessus]

Peux-tu préciser ces éléments ?"
```

**Attendre les réponses avant de continuer.**

### Étape 2️⃣ : PLAN D'IMPLÉMENTATION

```
Claude dit :
"Merci pour ces précisions. Voici mon plan d'implémentation :

**Architecture proposée :**
- Modèles : [Liste des modèles à créer/modifier]
- Services : [Liste des services avec leurs responsabilités]
- Endpoints : [Liste des endpoints avec méthodes HTTP]
- Composants : [Liste des composants Blazor]
- Hubs : [Modifications SignalR si nécessaire]

**Fichiers à créer :**
- [Liste exhaustive des nouveaux fichiers]

**Fichiers à modifier :**
- [Liste des fichiers existants impactés]

**Migrations :**
- [Changements en base de données]

**Étapes de développement :**
1. [Étape 1 avec justification]
2. [Étape 2 avec justification]
3. [...]

**Tests à implémenter :**
- Tests unitaires : [Liste]
- Tests d'intégration : [Liste]
- Tests UI : [Liste]
- Couverture estimée : X%

**Patterns utilisés :**
- [Liste des patterns et pourquoi]

**Choix techniques :**
- [Justification des choix importants]

Es-tu d'accord avec ce plan ? Y a-t-il des ajustements à faire ?"
```

**Attendre validation avant de continuer.**

### Étape 3️⃣ : GÉNÉRATION

Une fois le plan validé :

```
Claude génère :
1. Le code avec tous les patterns du projet
2. Les tests complets (≥80% coverage)
3. La documentation XML pour APIs publiques
4. Les commentaires pour code complexe
5. Les migrations EF Core si nécessaire
```

**Standards à respecter automatiquement :**
- ✅ Constructeurs primaires
- ✅ Accolades pour tous les blocs
- ✅ ILogger au lieu de Console.WriteLine
- ✅ Expression-bodied members pour méthodes simples
- ✅ API fluente pour configuration
- ✅ Nouveau scope pour vérifications DB dans tests
- ✅ Assertions xUnit natives (pas FluentAssertions)
- ✅ globalThis au lieu de window (JavaScript)
- ✅ Contrastes WCAG AA (CSS)
- ✅ Gestion des erreurs avec logging (JAMAIS de catch vide)

### Étape 4️⃣ : VÉRIFICATION ET RECOMMANDATIONS

```
Claude dit :
"Code généré avec succès ! ✅

**Fichiers créés :**
- [Liste]

**Fichiers modifiés :**
- [Liste]

**Check-list de qualité :**
- [x] Respect des guidelines
- [x] Tests complets (X% coverage)
- [x] Documentation XML
- [x] Pas de warnings attendus
- [x] Gestion des erreurs complète

**Points d'attention pour le review :**
- [Point 1 à vérifier]
- [Point 2 à vérifier]

**Commandes à exécuter :**
```bash
# Appliquer la migration
cd src/IrcChat.Api
dotnet ef migrations add [NomDeLaMigration]
dotnet ef database update

# Lancer les tests
dotnet test

# Vérifier la couverture
dotnet test --collect:"XPlat Code Coverage"
```

**Prochaines étapes suggérées :**
- [ ] Tester manuellement les scénarios
- [ ] Vérifier SonarCloud après push
- [ ] Mettre à jour CHANGELOG.md
- [ ] Ajouter documentation utilisateur si nécessaire
"
```

## 🚫 Règles spécifiques IrcChat - Ne JAMAIS générer sans avoir clarifié

### Authorization
```
Question obligatoire :
"Qui peut exécuter cette action ?"

Réponses attendues :
- Tous les utilisateurs authentifiés
- Admins uniquement
- Owner/Créateur de la ressource
- Combinaison (ex: admin OU owner)

Si réponse vague → Demander précision
```

### Validation des données
```
Question obligatoire :
"Quelles validations sur les données d'entrée ?"

Vérifier :
- Required fields
- String length (min/max)
- Format (email, URL, etc.)
- Plages de valeurs numériques
- Caractères autorisés
- Unicité (si applicable)

Si aucune validation spécifiée → Proposer des validations standard
```

### Gestion des erreurs
```
Règle absolue :
JAMAIS de catch vide ou sans logging

Toujours :
- Logger l'erreur avec contexte
- Décider si rethrow ou handling
- Utiliser le bon niveau de log (Error, Warning, Debug)
- Inclure les paramètres pertinents dans le log

Exception acceptable :
- Dispose/Cleanup (log Warning + justification)
- JS Interop optionnel (log Warning)
```

### Impact SignalR
```
Question obligatoire si fonctionnalité modifie des données :
"Cette action doit-elle notifier d'autres utilisateurs en temps réel ?"

Vérifier :
- Qui doit être notifié ? (All, Group, Caller, Client)
- Quel message/événement envoyer ?
- Impact sur ConnectionManager ?
```

### Logging
```
Règles de logging :
- Information : Opérations normales importantes
- Warning : Situations anormales mais gérables
- Error : Erreurs qui nécessitent attention
- Debug : Détails pour débogage (pas en production)

Format :
logger.LogInformation("Message avec {Param1} et {Param2}", param1, param2);

JAMAIS :
logger.LogInformation($"Message avec {param1}"); // ❌ Interpolation
Console.WriteLine("..."); // ❌ Console
```

## 🎨 Patterns obligatoires à vérifier

### Backend (C#)

```csharp
// ✅ Constructeurs primaires
public class MyService(ILogger<MyService> logger, IConfiguration config)
{
    public void Method() => logger.LogInformation("Hello");
}

// ✅ Accolades obligatoires
if (condition)
{
    DoSomething();
}

// ✅ ILogger avec paramètres structurés
logger.LogInformation("User {UserId} joined {Channel}", userId, channel);

// ✅ Expression-bodied members
public string GetName() => $"{FirstName} {LastName}";

// ✅ API fluente
services
    .AddDatabaseServices(configuration)
    .AddApplicationServices(configuration);

// ✅ Gestion des erreurs
try
{
    await DoSomething();
}
catch (Exception ex)
{
    logger.LogError(ex, "Error in DoSomething for {UserId}", userId);
    throw;
}
```

### Tests

```csharp
// ✅ Pattern AAA
[Fact]
public async Task MethodName_Scenario_ExpectedBehavior()
{
    // Arrange
    using var scope = _factory.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<ChatDbContext>();
    // ... setup

    // Act
    var response = await _client.PostAsJsonAsync("/api/endpoint", data);

    // Assert
    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    
    // ✅ CRITIQUE : Nouveau scope pour vérification DB
    using var verifyScope = _factory.Services.CreateScope();
    using var verifyContext = verifyScope.ServiceProvider.GetRequiredService<ChatDbContext>();
    var result = await verifyContext.Entities.FindAsync(id);
    Assert.NotNull(result);
}

// ✅ Assertions xUnit natives
Assert.Equal(expected, actual);
Assert.NotNull(value);
Assert.True(condition);
Assert.Contains(item, collection);
```

### Frontend (JavaScript)

```javascript
// ✅ globalThis au lieu de window
export function isMobileDevice() {
    if (globalThis.innerWidth <= 768) {
        return true;
    }
    
    if ('ontouchstart' in globalThis) {
        return true;
    }
    
    return false;
}
```

### CSS

```css
/* ✅ Contrastes WCAG AA minimum */
.button {
  background: #f56565;
  color: #000; /* Ratio: 10.35:1 - BON */
}

.text-muted {
  color: #aaa; /* Sur fond #1e1e1e - Ratio: 6.89:1 - BON */
}

/* ❌ Contraste insuffisant */
.bad-button {
  background: #f56565;
  color: #fff; /* Ratio: 3.16:1 - INSUFFISANT */
}
```

## 📚 Mode verbeux - Explications automatiques

Claude doit expliquer automatiquement :

### Pourquoi (Choix d'architecture)
```
"J'ai choisi d'utiliser [pattern/approche] parce que :
1. [Raison technique]
2. [Raison de maintenabilité]
3. [Cohérence avec code existant]"
```

### Comment (Code complexe)
```
"Ce code fonctionne en 3 étapes :
1. [Étape 1 avec explication]
2. [Étape 2 avec explication]
3. [Étape 3 avec explication]"
```

### Quels tests (Couverture)
```
"Les tests couvrent :
- Happy path : [Test X] vérifie [scénario]
- Validation : [Test Y] vérifie [scénario]
- Authorization : [Test Z] vérifie [scénario]
- Edge cases : [Test W] vérifie [scénario]

Ces tests garantissent [comportement attendu]."
```

### Où (Références)
```
"Ce pattern est similaire à :
- [Fichier existant 1] pour [aspect]
- [Fichier existant 2] pour [aspect]

J'ai réutilisé la même approche pour cohérence."
```

## ⚠️ Gestion des demandes vagues

Si la demande manque de détails, Claude doit :

### 1. Signaler explicitement
```
"⚠️ Ta demande manque de précisions sur plusieurs points importants."
```

### 2. Identifier les points manquants
```
"Points à clarifier :
- [ ] Authorization : qui peut exécuter cette action ?
- [ ] Validation : quelles règles sur les données ?
- [ ] Scénarios : quels cas d'usage concrets ?
- [ ] Tests : quels scénarios prioritaires ?"
```

### 3. Proposer un template
```
"Pour m'aider à générer le meilleur code, peux-tu compléter ce template ?

**Contexte métier :**
- Qui : [Type d'utilisateur]
- Quoi : [Action à effectuer]
- Pourquoi : [Problème résolu]
- Quand : [Contexte d'utilisation]

**Spécifications techniques :**
- Modèles : [Données manipulées]
- Services : [Logique nécessaire]
- Endpoints : [Routes API]
- UI : [Composants Blazor]

**Règles :**
- Validation : [Règles sur les données]
- Authorization : [Qui peut faire quoi]
- Erreurs : [Cas d'erreur possibles]

**Tests :**
1. Happy path : [Scénario normal]
2. Validation : [Données invalides]
3. Authorization : [Accès refusé]
4. Edge cases : [Cas limites]"
```

### 4. NE PAS générer sans réponses
```
"Je ne génère pas de code tant que ces points ne sont pas clarifiés,
pour éviter de faire des assumptions incorrectes."
```

## 🎯 Cas d'usage par type de fonctionnalité

### Nouvel endpoint API

**Questions automatiques :**
```
1. Méthode HTTP : GET, POST, PUT, DELETE ?
2. Route : /api/[resource]/[action] ?
3. Body/Query params : Quelles données en entrée ?
4. Response : Quel format de sortie ?
5. Authorization : Quel niveau d'accès ?
6. Validation : Quelles règles ?
7. Side effects : Impact sur autres données ? SignalR ?
```

**Tests requis :**
```
- Happy path (200/201)
- Validation (400)
- Authorization (401/403)
- Not Found (404)
- Edge cases spécifiques
```

### Nouveau service

**Questions automatiques :**
```
1. Responsabilité : Quelle logique métier ?
2. Dépendances : Quels autres services/repos ?
3. Méthodes : Quelles opérations publiques ?
4. État : Stateless ou stateful ?
5. Exceptions : Quelles erreurs possibles ?
```

**Tests requis :**
```
- Toutes les méthodes publiques
- Gestion des exceptions
- Interactions avec dépendances (mocks)
- Edge cases et valeurs limites
```

### Nouveau composant Blazor

**Questions automatiques :**
```
1. Responsabilité : Qu'affiche/fait le composant ?
2. Parameters : Quels inputs ?
3. Events : Quels callbacks ?
4. État : Quelles données locales ?
5. Lifecycle : Hooks nécessaires (OnInit, OnAfterRender) ?
6. Dépendances : Services injectés ?
```

**Tests requis :**
```
- Rendu initial avec différents parameters
- Interactions utilisateur (clics, inputs)
- États différents du composant
- Events/callbacks déclenchés
```

### Hub SignalR

**Questions automatiques :**
```
1. Méthodes : Quelles méthodes le client peut appeler ?
2. Événements : Quels événements broadcastés ?
3. Groupes : Gestion de groupes nécessaire ?
4. Authorization : Qui peut appeler quoi ?
5. Connexion : OnConnected/OnDisconnected logic ?
```

**Tests requis :**
```
- Connexion/Déconnexion
- Chaque méthode hub
- Broadcasting (All, Group, Client, Caller)
- Authorization
- Gestion des groupes
```

## 🔄 Exemples de conversation

### Exemple 1 : Demande vague

```
👤 Utilisateur :
"Ajoute un système de notifications"

🤖 Claude :
"⚠️ Ta demande manque de précisions. Avant de générer, j'ai besoin de clarifier :

**Type de notifications :**
- En temps réel (SignalR) ?
- Par email ?
- In-app seulement ?
- Push notifications navigateur ?

**Événements déclencheurs :**
- Quels événements créent des notifications ?
  □ Nouveau message dans un canal ?
  □ Mention (@username) ?
  □ Message privé ?
  □ Invitation à un canal ?
  □ Autre ?

**Persistance :**
- Les notifications doivent-elles être sauvegardées en DB ?
- Avec historique complet ?
- Marquage lu/non-lu ?
- Durée de conservation ?

**Interface utilisateur :**
- Badge avec compteur ?
- Liste déroulante dans la navbar ?
- Page dédiée ?
- Toast/popup ?

**Préférences utilisateur :**
- Les users peuvent-ils désactiver certaines notifs ?
- Paramètres de préférence ?

Peux-tu préciser ces points pour que je puisse proposer la meilleure architecture ?"
```

### Exemple 2 : Demande précise

```
👤 Utilisateur :
"Implémente un système de notification in-app avec SignalR.
Événements : nouveau message, mention, message privé.
Sauvegarde en DB avec marquage lu/non-lu.
UI : badge + liste déroulante."

🤖 Claude :
"Merci pour ces précisions ! Voici mon plan d'implémentation :

**Architecture proposée :**

*Modèles (Data/) :*
- Notification : Id, UserId, Type, Title, Message, IsRead, CreatedAt, RelatedEntityId
- NotificationType (enum) : NewMessage, Mention, PrivateMessage

*Services (Services/) :*
- NotificationService : CreateNotification, MarkAsRead, GetUnreadCount, GetUserNotifications
  - Responsabilités : CRUD sur notifications, logique métier

*Endpoints (Endpoints/) :*
- NotificationEndpoints.cs :
  - GET /api/notifications → Liste des notifications (paginée)
  - GET /api/notifications/unread-count → Compteur non-lues
  - PUT /api/notifications/{id}/mark-read → Marquer comme lue
  - PUT /api/notifications/mark-all-read → Tout marquer lu

*Hub (Hubs/) :*
- Modifier ChatHub pour envoyer notifications temps réel
- Nouvelle méthode : await Clients.User(userId).SendAsync("ReceiveNotification", notification)

*Composants (Client/Components/) :*
- NotificationBadge.razor : Badge avec compteur
- NotificationDropdown.razor : Liste déroulante
- NotificationItem.razor : Item individuel

**Fichiers à créer :**
1. src/IrcChat.Api/Models/Notification.cs
2. src/IrcChat.Api/Models/NotificationType.cs
3. src/IrcChat.Api/Services/NotificationService.cs
4. src/IrcChat.Api/Endpoints/NotificationEndpoints.cs
5. src/IrcChat.Api/Migrations/XXXXXX_AddNotifications.cs
6. src/IrcChat.Client/Components/NotificationBadge.razor
7. src/IrcChat.Client/Components/NotificationDropdown.razor
8. src/IrcChat.Client/Components/NotificationItem.razor
9. src/IrcChat.Shared/Models/NotificationDto.cs

**Fichiers à modifier :**
1. src/IrcChat.Api/Hubs/ChatHub.cs (ajouter envoi notifications)
2. src/IrcChat.Api/Data/ChatDbContext.cs (ajouter DbSet)
3. src/IrcChat.Api/Extensions/ServiceCollectionExtensions.cs (enregistrer NotificationService)
4. src/IrcChat.Api/Extensions/WebApplicationExtensions.cs (mapper NotificationEndpoints)
5. src/IrcChat.Client/Shared/MainLayout.razor (intégrer NotificationBadge)

**Tests à créer :**
1. tests/IrcChat.Api.Tests/Services/NotificationServiceTests.cs
2. tests/IrcChat.Api.Tests/Integration/NotificationEndpointsTests.cs
3. tests/IrcChat.Api.Tests/Hubs/ChatHubNotificationsTests.cs
4. tests/IrcChat.Client.Tests/Components/NotificationBadgeTests.cs
5. tests/IrcChat.Client.Tests/Components/NotificationDropdownTests.cs

**Scénarios de test :**
- Service : CreateNotification, MarkAsRead, GetUnreadCount, pagination
- Endpoint : GET notifications (200), Mark read (200), Authorization (401), NotFound (404)
- Hub : SendNotification to user, multiple users
- UI : Badge affiche count, dropdown affiche liste, click marque lu

**Étapes d'implémentation :**
1. Créer modèles + migration (base de données)
2. Implémenter NotificationService + tests unitaires
3. Créer endpoints API + tests d'intégration
4. Modifier ChatHub pour notifications temps réel + tests
5. Créer composants Blazor + tests UI
6. Intégrer dans MainLayout

**Choix techniques :**
- SignalR avec Clients.User() pour ciblage utilisateur spécifique
- Pagination côté serveur (20 notifications par page)
- Cache du unread count (refresh toutes les 30s)
- Soft delete des notifications (archivage après 30 jours)

Es-tu d'accord avec ce plan ?"

👤 Utilisateur :
"OK génère"

🤖 Claude :
[Génère tout le code + tests]

"✅ Code généré avec succès !

**Commandes à exécuter :**
```bash
cd src/IrcChat.Api
dotnet ef migrations add AddNotifications
dotnet ef database update
dotnet test
```

**Points d'attention :**
- Vérifier la performance de GetUnreadCount avec beaucoup de notifications
- Tester le ciblage SignalR avec plusieurs utilisateurs connectés
- Valider l'UI responsive du dropdown sur mobile

**Couverture estimée : 85%**"
```

## 📖 Ressources du projet à consulter

Avant de générer du code, Claude consulte automatiquement :

### Toujours
- `CODING_GUIDELINES.md` - Conventions de code
- `TEST_POLICY.md` - Politique de tests
- `TESTING_BEST_PRACTICES.md` - Bonnes pratiques de test

### Selon le contexte
- Backend : Fichiers dans `src/IrcChat.Api/`
- Frontend : Fichiers dans `src/IrcChat.Client/`
- Tests existants : Fichiers dans `tests/`
- Documentation : `README.md`, `CONTRIBUTING.md`

## 🎓 Apprentissage continu

Claude doit :
- **Apprendre** des patterns existants dans le code
- **Réutiliser** les solutions qui fonctionnent
- **Améliorer** en suggérant des optimisations
- **Documenter** les choix non-évidents

---

## 🚀 Activation

Pour activer ce mode, l'utilisateur peut simplement dire :

```
"Suis la config .claude/project-config.md"
```

Ou commencer sa demande par :

```
"Mode Socratic : [demande]"
```

Claude adoptera alors automatiquement le comportement défini dans cette configuration.