// src/IrcChat.Client/wwwroot/js/audioPlayer.js

/**
 * Module ES6 pour la gestion des sons de notification.
 * Utilise l'API Web Audio pour jouer un son de notification discret.
 * Exporté pour être importé dynamiquement depuis C#.
 */

let audioContext = null;
let audioBuffer = null;
let isAudioContextReady = false;

/**
 * Initialise le contexte audio après une interaction utilisateur.
 * Cette fonction doit être appelée suite à un clic ou autre gesture.
 */
async function initAudioContext() {
  if (audioContext && isAudioContextReady) {
    return true;
  }

  try {
    const AudioContextClass = globalThis.AudioContext || globalThis.webkitAudioContext;
    if (!AudioContextClass) {
      console.warn('[AudioPlayer] Web Audio API non supportée');
      return false;
    }

    if (!audioContext) {
      audioContext = new AudioContextClass();
    }

    // Tenter de reprendre le contexte (requis par politique navigateur)
    if (audioContext.state === 'suspended') {
      await audioContext.resume();
    }

    if (audioContext.state === 'running') {
      isAudioContextReady = true;
      return true;
    }

    return false;
  } catch (error) {
    console.warn('[AudioPlayer] Erreur lors de l\'initialisation du contexte audio:', error);
    return false;
  }
}

/**
 * Génère un son de notification type "touche de clavier".
 * Son très court avec attack rapide et composantes de bruit.
 */
function generateNotificationSound() {
  if (!audioContext) {
    return null;
  }

  try {
    // Créer un buffer très court (0.03 secondes = 30ms)
    const duration = 0.03;
    const sampleRate = audioContext.sampleRate;
    const bufferSize = sampleRate * duration;

    const buffer = audioContext.createBuffer(1, bufferSize, sampleRate);
    const channelData = buffer.getChannelData(0);

    // Son de clavier = combinaison de fréquences + bruit
    const freq1 = 50; // Fréquence principale (haute)
    const freq2 = 400; // Harmonique (plus haute)

    for (let i = 0; i < bufferSize; i++) {
      const t = i / sampleRate;

      // Deux sinusoïdes pour richesse du son
      const tone1 = Math.sin(2 * Math.PI * freq1 * t) * 0.4;
      const tone2 = Math.sin(2 * Math.PI * freq2 * t) * 0.2;

      // Ajout de bruit blanc (pour le "clic" mécanique)
      const noise = (Math.random() * 2 - 1) * 0.15;

      // Combinaison
      const value = tone1 + tone2 + noise;

      // Envelope très rapide (attack instantané, decay rapide)
      const attack = Math.min(1, i / (sampleRate * 0.002)); // 2ms attack
      const decay = Math.exp(-t * 15); // Decay rapide

      channelData[i] = value * attack * decay * 0.5;
    }

    return buffer;
  } catch (error) {
    console.warn('[AudioPlayer] Erreur lors de la génération du son:', error);
    return null;
  }
}

/**
 * Prépare le son de notification.
 */
function prepareNotificationSound() {
  if (audioBuffer) {
    return;
  }

  audioBuffer = generateNotificationSound();
}

/**
 * Joue le son de notification.
 * Fonction exportée pour être appelée depuis C#.
 * 
 * Note: Le premier appel peut échouer si l'utilisateur n'a pas encore interagi avec la page.
 * C'est normal et conforme à la politique des navigateurs modernes.
 */
export async function playSound() {
  try {
    // Tenter d'initialiser/reprendre le contexte audio
    const ready = await initAudioContext();

    if (!ready) {
      // Silencieux : l'utilisateur n'a pas encore interagi avec la page
      // Le son jouera après la première interaction (clic, etc.)
      return;
    }

    // Préparer le buffer si nécessaire
    prepareNotificationSound();

    if (!audioContext || !audioBuffer) {
      return;
    }

    // Créer une source audio
    const source = audioContext.createBufferSource();
    source.buffer = audioBuffer;

    // Gain node pour contrôler le volume (fixe à 0.7 = discret)
    const gainNode = audioContext.createGain();
    gainNode.gain.value = 0.7;

    // Connecter: source -> gain -> destination
    source.connect(gainNode);
    gainNode.connect(audioContext.destination);

    // Jouer
    source.start(0);
  } catch (error) {
    // Silencieux : ne pas polluer la console avec des erreurs attendues
    // L'utilisateur entendra le son dès qu'il interagira avec la page
  }
}
