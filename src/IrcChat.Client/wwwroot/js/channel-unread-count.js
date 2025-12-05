// src/IrcChat.Client/wwwroot/js/channel-unread-count.js

/**
 * Récupère les compteurs de messages non lus depuis sessionStorage
 * @returns {Object} Dictionnaire des compteurs par salon (ex: { "general": 5, "random": 12 })
 */
export function getUnreadCounts() {
  try {
    const data = globalThis.sessionStorage.getItem('channelUnreadCounts');
    if (!data) {
      return {};
    }
    return JSON.parse(data);
  } catch (error) {
    console.error('Erreur lors de la lecture des compteurs non lus:', error);
    return {};
  }
}

/**
 * Sauvegarde les compteurs de messages non lus dans sessionStorage
 * @param {Object} counts - Dictionnaire des compteurs par salon
 */
export function saveUnreadCounts(counts) {
  try {
    const data = JSON.stringify(counts);
    globalThis.sessionStorage.setItem('channelUnreadCounts', data);
  } catch (error) {
    console.error('Erreur lors de la sauvegarde des compteurs non lus:', error);
  }
}
