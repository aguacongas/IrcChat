// Module pour accès caméra et capture de photos
let currentStream = null;
/**

Demande l'accès à la caméra et retourne un stream vidéo.
@returns {Promise<MediaStream>} Stream vidéo
*/
export async function startCamera() {
  try {
    if (currentStream) {
      stopCamera();
    }
    const constraints = {
      video: {
        width: { ideal: 1920 },
        height: { ideal: 1080 },
        facingMode: 'user' // Caméra frontale par défaut
      },
      audio: false
    };
    currentStream = await navigator.mediaDevices.getUserMedia(constraints);
    console.log('✅ Caméra démarrée');
    return currentStream;
  } catch (error) {
    console.error('❌ Erreur accès caméra:', error);
    throw new Error(getCameraErrorMessage(error));
  }
}

/**

Arrête le stream de la caméra.
*/
export function stopCamera() {
  if (currentStream) {
    currentStream.getTracks().forEach(track => track.stop());
    currentStream = null;
    console.log('✅ Caméra arrêtée');
  }
}

/**

Attache le stream vidéo à un élément video HTML.
@param {string} videoElementId ID de l'élément <video>
@param {MediaStream} stream Stream vidéo
*/
export function attachStreamToVideo(videoElementId, stream) {
  const videoElement = document.getElementById(videoElementId);
  if (!videoElement) {
    throw new Error(`Élément video "${videoElementId}" introuvable`);
  }

  videoElement.srcObject = stream;
  videoElement.play();
  console.log('✅ Stream attaché au video element');
}
/**

Capture une photo depuis un élément video et retourne le base64.
@param {string} videoElementId ID de l'élément <video>
@returns {string} Image en base64 (data URL)
*/
export function capturePhotoFromVideo(videoElementId) {
  const videoElement = document.getElementById(videoElementId);
  if (!videoElement) {
    throw new Error(`Élément video "${videoElementId}" introuvable`);
  }

  // Créer un canvas avec les dimensions de la vidéo
  const canvas = document.createElement('canvas');
  canvas.width = videoElement.videoWidth;
  canvas.height = videoElement.videoHeight;
  // Dessiner le frame actuel sur le canvas
  const context = canvas.getContext('2d');
  context.drawImage(videoElement, 0, 0, canvas.width, canvas.height);
  // Convertir en base64 (JPEG, qualité 85%)
  const base64 = canvas.toDataURL('image/jpeg', 0.85);
  console.log('✅ Photo capturée:', base64.substring(0, 50) + '...');
  return base64;
}
/**

Vérifie si l'API MediaDevices est disponible.
@returns {boolean} True si disponible
*/
export function isCameraAvailable() {
  return !!(navigator.mediaDevices?.getUserMedia);
}

/**

Retourne un message d'erreur localisé selon le type d'erreur.
*/
function getCameraErrorMessage(error) {
  if (error.name === 'NotAllowedError' || error.name === 'PermissionDeniedError') {
    return `Permission caméra refusée. Autorisez l'accès dans les paramètres du navigateur.`;
  }
  if (error.name === 'NotFoundError' || error.name === 'DevicesNotFoundError') {
    return 'Aucune caméra détectée sur cet appareil.';
  }
  if (error.name === 'NotReadableError' || error.name === 'TrackStartError') {
    return 'Caméra déjà utilisée par une autre application.';
  }
  if (error.name === 'OverconstrainedError') {
    return 'Les contraintes de la caméra ne peuvent pas être satisfaites.';
  }
  return `Erreur d'accès caméra: ${error.message}`;
}
