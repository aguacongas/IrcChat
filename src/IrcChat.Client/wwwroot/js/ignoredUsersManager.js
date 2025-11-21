/**
 * Module de gestion des utilisateurs ignorés via IndexedDB
 * Utilise la même base de données que userIdManager.js
 * Stockage dans la table 'ignored-users'
 */

const DB_NAME = 'IrcChatDb';
const STORE_NAME = 'ignored-users';
const DB_VERSION = 1;

let db = null;

/**
 * Initialise la connexion à la base de données IndexedDB
 */
async function initializeDatabase() {
  if (db) return;

  return new Promise((resolve, reject) => {
    const request = indexedDB.open(DB_NAME, DB_VERSION);

    request.onerror = () => {
      console.error('Erreur lors de l\'ouverture de la base de données IndexedDB');
      reject(request.error);
    };

    request.onsuccess = () => {
      db = request.result;
      console.log('Base de données IndexedDB ouverte avec succès');
      resolve(db);
    };

    request.onupgradeneeded = (event) => {
      const newDb = event.target.result;

      // Créer la table des utilisateurs ignorés si elle n'existe pas
      if (!newDb.objectStoreNames.contains(STORE_NAME)) {
        const store = newDb.createObjectStore(STORE_NAME, { keyPath: 'userId' });
        store.createIndex('userId', 'userId', { unique: true });
        store.createIndex('createdAt', 'createdAt', { unique: false });
        console.log(`Table '${STORE_NAME}' créée dans IndexedDB`);
      }
    };
  });
}

/**
 * Vérifie si un utilisateur est ignoré par son ID
 * @param {string} userId - L'ID de l'utilisateur à vérifier
 * @returns {Promise<boolean>}
 */
export async function isUserIgnored(userId) {
  try {
    await initializeDatabase();

    return new Promise((resolve, reject) => {
      const transaction = db.transaction([STORE_NAME], 'readonly');
      const store = transaction.objectStore(STORE_NAME);
      const request = store.get(userId);

      request.onerror = () => {
        console.error(`Erreur lors de la vérification du statut pour ${userId}`);
        reject(request.error);
      };

      request.onsuccess = () => {
        const exists = request.result !== undefined;
        console.debug(`Utilisateur ${userId} ignoré: ${exists}`);
        resolve(exists);
      };
    });
  } catch (error) {
    console.error('Erreur dans isUserIgnored:', error);
    return false;
  }
}

/**
 * Ajoute un utilisateur à la liste des ignorés
 * @param {string} userId - L'ID de l'utilisateur à ignorer
 */
export async function ignoreUser(userId) {
  try {
    await initializeDatabase();

    return new Promise((resolve, reject) => {
      const transaction = db.transaction([STORE_NAME], 'readwrite');
      const store = transaction.objectStore(STORE_NAME);

      const request = store.put({
        userId: userId,
        createdAt: new Date().toISOString()
      });

      request.onerror = () => {
        console.error(`Erreur lors de l'ignorance de l'utilisateur ${userId}`);
        reject(request.error);
      };

      request.onsuccess = () => {
        console.log(`Utilisateur ${userId} ajouté à la liste des ignorés`);
        resolve();
      };
    });
  } catch (error) {
    console.error('Erreur dans ignoreUser:', error);
    throw error;
  }
}

/**
 * Retire un utilisateur de la liste des ignorés
 * @param {string} userId - L'ID de l'utilisateur à dés-ignorer
 */
export async function unignoreUser(userId) {
  try {
    await initializeDatabase();

    return new Promise((resolve, reject) => {
      const transaction = db.transaction([STORE_NAME], 'readwrite');
      const store = transaction.objectStore(STORE_NAME);
      const request = store.delete(userId);

      request.onerror = () => {
        console.error(`Erreur lors de la dés-ignorance de l'utilisateur ${userId}`);
        reject(request.error);
      };

      request.onsuccess = () => {
        console.log(`Utilisateur ${userId} retiré de la liste des ignorés`);
        resolve();
      };
    });
  } catch (error) {
    console.error('Erreur dans unignoreUser:', error);
    throw error;
  }
}

/**
 * Obtient tous les utilisateurs ignorés
 * @returns {Promise<string[]>} Array des IDs des utilisateurs ignorés
 */
export async function getAllIgnoredUsers() {
  try {
    await initializeDatabase();

    return new Promise((resolve, reject) => {
      const transaction = db.transaction([STORE_NAME], 'readonly');
      const store = transaction.objectStore(STORE_NAME);
      const request = store.getAll();

      request.onerror = () => {
        console.error('Erreur lors de la récupération des utilisateurs ignorés');
        reject(request.error);
      };

      request.onsuccess = () => {
        const ignoredUsers = request.result.map(record => record.userId);
        console.debug(`${ignoredUsers.length} utilisateurs ignorés récupérés`);
        resolve(ignoredUsers);
      };
    });
  } catch (error) {
    console.error('Erreur dans getAllIgnoredUsers:', error);
    return [];
  }
}

/**
 * Vide la liste de tous les utilisateurs ignorés
 */
export async function clearAllIgnoredUsers() {
  try {
    await initializeDatabase();

    return new Promise((resolve, reject) => {
      const transaction = db.transaction([STORE_NAME], 'readwrite');
      const store = transaction.objectStore(STORE_NAME);
      const request = store.clear();

      request.onerror = () => {
        console.error('Erreur lors du vidage de la liste des ignorés');
        reject(request.error);
      };

      request.onsuccess = () => {
        console.log('Liste de tous les utilisateurs ignorés supprimée');
        resolve();
      };
    });
  } catch (error) {
    console.error('Erreur dans clearAllIgnoredUsers:', error);
    throw error;
  }
}
