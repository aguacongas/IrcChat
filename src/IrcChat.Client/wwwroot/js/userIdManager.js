// wwwroot/js/userIdManager.js

/**
 * Gestionnaire d'identifiant utilisateur pour les messages privés
 * Utilise IndexedDB pour persister l'ID entre les sessions
 */

const DB_NAME = 'IrcChatDB';
const DB_VERSION = 1;
const STORE_NAME = 'settings';
const USER_ID_KEY = 'userId';

/**
 * Ouvre la base de données IndexedDB
 * @returns {Promise<IDBDatabase>}
 */
function openDatabase() {
  return new Promise((resolve, reject) => {
    const request = globalThis.indexedDB.open(DB_NAME, DB_VERSION);

    request.onerror = () => {
      console.error('Erreur lors de l\'ouverture de IndexedDB:', request.error);
      reject(request.error);
    };

    request.onupgradeneeded = (event) => {
      const db = event.target.result;
      if (!db.objectStoreNames.contains(STORE_NAME)) {
        db.createObjectStore(STORE_NAME);
        console.log('Object store créé:', STORE_NAME);
      }
    };

    request.onsuccess = (event) => {
      resolve(event.target.result);
    };
  });
}

/**
 * Récupère ou génère un UserId unique
 * @returns {Promise<string>} L'UserId (GUID)
 */
export async function getUserId() {
  try {
    const db = await openDatabase();
    const transaction = db.transaction([STORE_NAME], 'readwrite');
    const store = transaction.objectStore(STORE_NAME);

    return new Promise((resolve, reject) => {
      const getRequest = store.get(USER_ID_KEY);

      getRequest.onsuccess = () => {
        if (getRequest.result) {
          console.log('UserId existant récupéré:', getRequest.result);
          resolve(getRequest.result);
        } else {
          // Générer un nouveau GUID
          const newUserId = crypto.randomUUID();
          console.log('Nouveau UserId généré:', newUserId);

          const putRequest = store.put(newUserId, USER_ID_KEY);

          putRequest.onsuccess = () => {
            resolve(newUserId);
          };

          putRequest.onerror = () => {
            console.error('Erreur lors de la sauvegarde du UserId:', putRequest.error);
            reject(putRequest.error);
          };
        }
      };

      getRequest.onerror = () => {
        console.error('Erreur lors de la récupération du UserId:', getRequest.error);
        reject(getRequest.error);
      };
    });
  } catch (error) {
    console.error('Erreur dans getUserId:', error);
    throw error;
  }
}

/**
 * Supprime le UserId stocké (pour déconnexion complète)
 * @returns {Promise<void>}
 */
export async function clearUserId() {
  try {
    const db = await openDatabase();
    const transaction = db.transaction([STORE_NAME], 'readwrite');
    const store = transaction.objectStore(STORE_NAME);

    return new Promise((resolve, reject) => {
      const deleteRequest = store.delete(USER_ID_KEY);

      deleteRequest.onsuccess = () => {
        console.log('UserId supprimé');
        resolve();
      };

      deleteRequest.onerror = () => {
        console.error('Erreur lors de la suppression du UserId:', deleteRequest.error);
        reject(deleteRequest.error);
      };
    });
  } catch (error) {
    console.error('Erreur dans clearUserId:', error);
    throw error;
  }
}

/**
 * Définit manuellement un UserId (pour utilisateurs réservés)
 * @param {string} userId - L'UserId à définir
 * @returns {Promise<void>}
 */
export async function setUserId(userId) {
  try {
    const db = await openDatabase();
    const transaction = db.transaction([STORE_NAME], 'readwrite');
    const store = transaction.objectStore(STORE_NAME);

    return new Promise((resolve, reject) => {
      const putRequest = store.put(userId, USER_ID_KEY);

      putRequest.onsuccess = () => {
        console.log('UserId défini:', userId);
        resolve();
      };

      putRequest.onerror = () => {
        console.error('Erreur lors de la définition du UserId:', putRequest.error);
        reject(putRequest.error);
      };
    });
  } catch (error) {
    console.error('Erreur dans setUserId:', error);
    throw error;
  }
} 
