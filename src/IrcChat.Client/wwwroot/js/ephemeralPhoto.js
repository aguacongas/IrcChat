// Sécurité pour les photos éphémères

/**
 * Bloque l'ouverture des DevTools pendant l'affichage de la photo.
 */
export function blockDevTools() {
  const check = () => {
    const threshold = 160;
    const widthThreshold = globalThis.outerWidth - globalThis.innerWidth > threshold;
    const heightThreshold = globalThis.outerHeight - globalThis.innerHeight > threshold;

    if (widthThreshold || heightThreshold) {
      console.warn('⚠️ DevTools détecté - Fermer pour continuer');
      document.body.innerHTML = '<div style="display:flex;align-items:center;justify-content:center;height:100vh;font-size:24px;">⚠️ Fermer DevTools pour voir la photo</div>';
    }
  };

  const interval = setInterval(check, 100);

  // Nettoyer après 4 secondes (durée de vie de la photo + 1s)
  setTimeout(() => clearInterval(interval), 4000);
}

/**
 * Détecte les tentatives de screenshot.
 */
export function detectScreenshot() {
  let screenshotDetected = false;

  // Page Visibility API
  const handleVisibilityChange = () => {
    if (document.hidden && !screenshotDetected) {
      screenshotDetected = true;
      console.warn('📸 Possible screenshot détecté (changement de focus)');
    }
  };

  document.addEventListener('visibilitychange', handleVisibilityChange);

  // beforeunload (changement d'onglet)
  const handleBeforeUnload = () => {
    if (!screenshotDetected) {
      screenshotDetected = true;
      console.warn('📸 Possible screenshot détecté (beforeunload)');
    }
  };

  globalThis.addEventListener('beforeunload', handleBeforeUnload);

  // Nettoyer après 4 secondes
  setTimeout(() => {
    document.removeEventListener('visibilitychange', handleVisibilityChange);
    globalThis.removeEventListener('beforeunload', handleBeforeUnload);
  }, 4000);
}

/**
 * Détruit les données de l'image de la mémoire.
 */
export function destroyImageData(elementId) {
  const img = document.getElementById(elementId);
  if (img) {
    // Remplacer par une image vide
    img.src = 'data:image/gif;base64,R0lGODlhAQABAIAAAAAAAP///yH5BAEAAAAALAAAAAABAAEAAAIBRAA7';
    img.remove();
    console.log('✅ Image détruite de la mémoire');
  }
}

/**
 * Ajoute un watermark sur un canvas (optionnel, si utilisation de Canvas).
 */
export function addWatermark(canvasId, text) {
  const canvas = document.getElementById(canvasId);
  if (!canvas) {
    return;
  }

  const ctx = canvas.getContext('2d');
  if (!ctx) {
    return;
  }

  // Configurer le texte
  ctx.font = '40px Arial';
  ctx.fillStyle = 'rgba(255, 255, 255, 0.3)';
  ctx.textAlign = 'center';

  // Rotation pour diagonale
  ctx.save();
  ctx.translate(canvas.width / 2, canvas.height / 2);
  ctx.rotate(-45 * Math.PI / 180);
  ctx.fillText(text, 0, 0);
  ctx.restore();

  console.log('✅ Watermark ajouté');
}
