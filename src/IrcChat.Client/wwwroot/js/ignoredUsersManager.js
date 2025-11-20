// ignoredUsersManager.js

// Function to initialize IndexedDB
function initDB() {
    return new Promise((resolve, reject) => {
        const request = indexedDB.open('ignoredUsersDB', 1);

        request.onupgradeneeded = (event) => {
            const db = event.target.result;
            db.createObjectStore('ignoredUsers', { keyPath: 'id' });
        };

        request.onsuccess = (event) => {
            resolve(event.target.result);
        };

        request.onerror = (event) => {
            reject('Database error: ' + event.target.errorCode);
        };
    });
}

// Function to add an ignored user
async function addIgnoredUser(userId) {
    const db = await initDB();
    const transaction = db.transaction(['ignoredUsers'], 'readwrite');
    const store = transaction.objectStore('ignoredUsers');
    const userRecord = { id: userId };
    store.add(userRecord);
}

// Function to remove an ignored user
async function removeIgnoredUser(userId) {
    const db = await initDB();
    const transaction = db.transaction(['ignoredUsers'], 'readwrite');
    const store = transaction.objectStore('ignoredUsers');
    store.delete(userId);
}

// Function to get all ignored users
async function getIgnoredUsers() {
    return new Promise((resolve, reject) => {
        const dbRequest = indexedDB.open('ignoredUsersDB', 1);

        dbRequest.onsuccess = (event) => {
            const db = event.target.result;
            const transaction = db.transaction(['ignoredUsers'], 'readonly');
            const store = transaction.objectStore('ignoredUsers');
            const request = store.getAll();

            request.onsuccess = (event) => {
                resolve(event.target.result);
            };

            request.onerror = (event) => {
                reject('Error fetching ignored users: ' + event.target.errorCode);
            };
        };
    });
}

// Example usage:
// (async () => {
//     await addIgnoredUser('user123');
//     const users = await getIgnoredUsers();
//     console.log(users);
//     await removeIgnoredUser('user123');
// })();
