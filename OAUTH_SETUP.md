# Configuration OAuth 2.0 - IRC Chat

Ce guide explique comment obtenir les identifiants OAuth pour chaque provider.

## 📋 Table des matières

- [Google](#-google)
- [Microsoft](#-microsoft)
- [Facebook](#-facebook)
- [Configuration locale](#-configuration-locale)

---

## 🔵 Google

### 1. Créer un projet Google Cloud

1. Aller sur [Google Cloud Console](https://console.cloud.google.com/)
2. Cliquer sur **Créer un projet**
3. Entrer un nom (ex: `IRC Chat`)
4. Cliquer sur **Créer**

### 2. Activer l'API Google+ (deprecated) ou utiliser OAuth 2.0

1. Dans la console, aller sur **API et services** > **Bibliothèque**
2. Chercher **Google+ API** ou **OAuth 2.0 consent screen**
3. Cliquer sur **Activer**

### 3. Configurer l'écran de consentement OAuth

1. Aller sur **API et services** > **Écran de consentement OAuth**
2. Sélectionner **Externe** pour le type d'utilisateur
3. Remplir les informations :
   - **Nom de l'application** : `IRC Chat`
   - **E-mail de support** : ton email
   - **Contacts administrateurs** : ton email
4. Cliquer sur **Enregistrer et continuer**
5. Ignorer les scopes, cliquer sur **Enregistrer et continuer**
6. Ajouter des utilisateurs de test (ton compte Google)
7. Cliquer sur **Enregistrer et continuer**

### 4. Créer les identifiants OAuth

1. Aller sur **API et services** > **Identifiants**
2. Cliquer sur **+ Créer les identifiants** > **ID client OAuth**
3. Choisir **Application web**
4. Remplir les informations :
   - **Nom** : `IRC Chat Web`
   - **URI JavaScript autorisés** :
     ```
     https://localhost:7002
     https://mondomaine.com (en production)
     ```
   - **URI de redirection autorisés** :
     ```
     https://localhost:7002/oauth-login
     https://mondomaine.com/oauth-login (en production)
     ```
5. Cliquer sur **Créer**
6. Copier le **Client ID** et le **Secret client**

### 5. Configuration dans IRC Chat

```bash
# Développement
dotnet user-secrets set "OAuth:Google:ClientId" "YOUR_CLIENT_ID.apps.googleusercontent.com"
dotnet user-secrets set "OAuth:Google:ClientSecret" "YOUR_CLIENT_SECRET"

# Production - Utiliser des variables d'environnement
export OAUTH_GOOGLE_CLIENTID="YOUR_CLIENT_ID"
export OAUTH_GOOGLE_CLIENTSECRET="YOUR_CLIENT_SECRET"
```

### 📝 Scopes utilisés

```
openid email profile
```

---

## 🟢 Microsoft

### 1. Créer une application Azure AD

1. Aller sur [Azure Portal](https://portal.azure.com/)
2. Aller sur **Azure Active Directory** > **Enregistrements d'applications**
3. Cliquer sur **+ Nouvelle inscription**
4. Remplir les informations :
   - **Nom** : `IRC Chat`
   - **Types de comptes pris en charge** : `Comptes personnels Microsoft uniquement`
   - **URI de redirection** :
     ```
     Web: https://localhost:7002/oauth-login
     ```
5. Cliquer sur **Inscrire**

### 2. Créer un secret client

1. Dans l'application, aller sur **Certificats et secrets**
2. Cliquer sur **+ Nouveau secret client**
3. Remplir :
   - **Description** : `IRC Chat Web`
   - **Expire** : `24 mois` (ou personnalisé)
4. Cliquer sur **Ajouter**
5. **COPIER IMMÉDIATEMENT** la valeur du secret (tu ne pourras pas la voir après!)

### 3. Ajouter les autorisations

1. Aller sur **Autorisations de l'API**
2. Cliquer sur **+ Ajouter une autorisation**
3. Sélectionner **Microsoft Graph**
4. Sélectionner **Autorisations déléguées**
5. Chercher et ajouter :
   - `openid`
   - `email`
   - `profile`
   - `User.Read`
6. Cliquer sur **Ajouter des autorisations**

### 4. Configuration dans IRC Chat

```bash
# Développement
dotnet user-secrets set "OAuth:Microsoft:ClientId" "YOUR_APPLICATION_ID"
dotnet user-secrets set "OAuth:Microsoft:ClientSecret" "YOUR_CLIENT_SECRET_VALUE"

# Production - Utiliser des variables d'environnement
export OAUTH_MICROSOFT_CLIENTID="YOUR_APPLICATION_ID"
export OAUTH_MICROSOFT_CLIENTSECRET="YOUR_CLIENT_SECRET_VALUE"
```

### 📝 Endpoints utilisés

```
Authorization: https://login.microsoftonline.com/consumers/oauth2/v2.0/authorize
Token: https://login.microsoftonline.com/consumers/oauth2/v2.0/token
User Info: https://graph.microsoft.com/v1.0/me
```

---

## 🔴 Facebook

### 1. Créer une application Facebook

1. Aller sur [Facebook Developers](https://developers.facebook.com/)
2. Aller sur **Mes applications**
3. Cliquer sur **+ Créer une application**
4. Choisir **Autre** comme type
5. Remplir le formulaire :
   - **Nom de l'application** : `IRC Chat`
   - **Email du contact** : ton email
   - **Objectif de l'application** : Cocher la case
6. Cliquer sur **Créer une application**

### 2. Configurer Facebook Login

1. Dans l'application, cliquer sur **+ Ajouter un produit**
2. Chercher **Facebook Login** et cliquer sur **Configurer**
3. Choisir **Web**
4. Créer une nouvelle application web ou utiliser l'existante

### 3. Configurer les URI de redirection

1. Aller sur **Paramètres** > **Paramètres de base**
2. Copier l'**ID d'application** et le **Secret de l'application**
3. Aller sur **Produits** > **Facebook Login** > **Paramètres**
4. Ajouter dans **URI de redirection OAuth valides** :
   ```
   https://localhost:7002/oauth-login
   https://mondomaine.com/oauth-login (en production)
   ```
5. Sauvegarder les modifications

### 4. Configurer les autorisations

1. Aller sur **Rôles** > **Rôles de test**
2. Ajouter un compte de test pour toi-même

### 5. Configuration dans IRC Chat

```bash
# Développement
dotnet user-secrets set "OAuth:Facebook:AppId" "YOUR_APP_ID"
dotnet user-secrets set "OAuth:Facebook:AppSecret" "YOUR_APP_SECRET"

# Production - Utiliser des variables d'environnement
export OAUTH_FACEBOOK_APPID="YOUR_APP_ID"
export OAUTH_FACEBOOK_APPSECRET="YOUR_APP_SECRET"
```

### 📝 Scopes utilisés

```
email public_profile
```

### ⚠️ Note importante

Facebook demande une révision pour la production. En développement/test, tu dois :
1. Ajouter ton compte comme **testeur** ou **développeur**
2. Utiliser le mode **Développement** de l'application

---

## 🔧 Configuration locale

### Avec `dotnet user-secrets`

```bash
# Depuis le dossier src/IrcChat.Api

# Google
dotnet user-secrets set "OAuth:Google:ClientId" "xxx.apps.googleusercontent.com"
dotnet user-secrets set "OAuth:Google:ClientSecret" "xxx"

# Microsoft
dotnet user-secrets set "OAuth:Microsoft:ClientId" "xxx"
dotnet user-secrets set "OAuth:Microsoft:ClientSecret" "xxx"

# Facebook
dotnet user-secrets set "OAuth:Facebook:AppId" "xxx"
dotnet user-secrets set "OAuth:Facebook:AppSecret" "xxx"
```

### Avec `appsettings.json` (⚠️ NE PAS COMMITER!)

```json
{
  "OAuth": {
    "Google": {
      "ClientId": "xxx.apps.googleusercontent.com",
      "ClientSecret": "xxx"
    },
    "Microsoft": {
      "ClientId": "xxx",
      "ClientSecret": "xxx"
    },
    "Facebook": {
      "AppId": "xxx",
      "AppSecret": "xxx"
    }
  }
}
```

### Avec variables d'environnement

```bash
# Linux/Mac
export OAUTH_GOOGLE_CLIENTID="xxx"
export OAUTH_GOOGLE_CLIENTSECRET="xxx"
export OAUTH_MICROSOFT_CLIENTID="xxx"
export OAUTH_MICROSOFT_CLIENTSECRET="xxx"
export OAUTH_FACEBOOK_APPID="xxx"
export OAUTH_FACEBOOK_APPSECRET="xxx"

# Windows PowerShell
$env:OAUTH_GOOGLE_CLIENTID="xxx"
$env:OAUTH_GOOGLE_CLIENTSECRET="xxx"
# etc...
```

---

## 🚀 Configuration en production

### Azure / AWS / Heroku

Utiliser les variables d'environnement du service :

```bash
# Ajouter les variables d'environnement
OAUTH_GOOGLE_CLIENTID=xxx
OAUTH_GOOGLE_CLIENTSECRET=xxx
OAUTH_MICROSOFT_CLIENTID=xxx
OAUTH_MICROSOFT_CLIENTSECRET=xxx
OAUTH_FACEBOOK_APPID=xxx
OAUTH_FACEBOOK_APPSECRET=xxx
```

### appsettings.production.json

```json
{
  "OAuth": {
    "Google": {
      "ClientId": "${OAUTH_GOOGLE_CLIENTID}",
      "ClientSecret": "${OAUTH_GOOGLE_CLIENTSECRET}"
    },
    "Microsoft": {
      "ClientId": "${OAUTH_MICROSOFT_CLIENTID}",
      "ClientSecret": "${OAUTH_MICROSOFT_CLIENTSECRET}"
    },
    "Facebook": {
      "AppId": "${OAUTH_FACEBOOK_APPID}",
      "AppSecret": "${OAUTH_FACEBOOK_APPSECRET}"
    }
  }
}
```

---

## 🔐 Bonnes pratiques de sécurité

### ✅ À FAIRE

- ✅ Jamais commiter les secrets dans Git
- ✅ Utiliser `dotnet user-secrets` en développement
- ✅ Utiliser les variables d'environnement en production
- ✅ Régulièrement rotationner les secrets
- ✅ Utiliser des URIs HTTPS en production
- ✅ Utiliser PKCE pour les applications web
- ✅ Valider les `state` parameters

### ❌ À NE PAS FAIRE

- ❌ Ne pas mettre les secrets dans `appsettings.json`
- ❌ Ne pas les publier sur GitHub
- ❌ Ne pas utiliser HTTP en production
- ❌ Ne pas restituer les secrets aux clients
- ❌ Ne pas exposer les secrets dans les logs

---

## ✅ Vérifier la configuration

```bash
# Tester que les secrets sont chargés
cd src/IrcChat.Api
dotnet user-secrets list

# Doit afficher quelque chose comme :
# OAuth:Google:ClientId = xxx
# OAuth:Google:ClientSecret = xxx
# etc...
```

---

## 🆘 Dépannage

### "Invalid Client ID"

- Vérifier que l'ID est correct
- Vérifier que les URIs de redirection correspondent
- Vérifier que la clé de configuration est correcte

### "Redirect URI mismatch"

- L'URI doit être **exactement** la même que celle enregistrée
- Vérifier le protocole (http vs https)
- Vérifier le port
- Vérifier la casse

### "Invalid secret"

- Vérifier que le secret est complet (parfois des caractères manquent)
- Pour Microsoft/Facebook, s'assurer d'avoir copié le secret **immédiatement** après création
- Créer un nouveau secret si nécessaire

### "Code expired"

- Le code OAuth a une courte durée de vie (généralement 10 minutes)
- Vérifier que `ExchangeCodeForTokenAsync` est appelé rapidement après
- Vérifier les logs pour voir s'il y a des délais

---

## 📖 Ressources supplémentaires

- [Google OAuth 2.0 Documentation](https://developers.google.com/identity/protocols/oauth2)
- [Microsoft Identity Platform](https://learn.microsoft.com/en-us/azure/active-directory/develop/)
- [Facebook Login Documentation](https://developers.facebook.com/docs/facebook-login)
- [OAuth 2.0 RFC 6749](https://tools.ietf.org/html/rfc6749)
- [PKCE RFC 7636](https://tools.ietf.org/html/rfc7636)