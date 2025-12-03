// src/IrcChat.Client/wwwroot/js/scroll-helper.js

/**
 * Fait défiler un élément jusqu'en bas
 * @param {HTMLElement} element - L'élément à faire défiler
 */
export function scrollToBottom(element) {
  if (element) {
    element.scrollTop = element.scrollHeight;
  }
}

/**
 * Vérifie si un élément est scrollé jusqu'en bas
 * @param {HTMLElement} element - L'élément à vérifier
 * @returns {boolean} - True si l'élément est en bas
 */
export function isScrolledToBottom(element) {
  if (!element) {
    return true;
  }

  const threshold = 10; // pixels de tolérance
  return Math.abs(
    element.scrollHeight - element.clientHeight - element.scrollTop
  ) < threshold;
}

/**
 * Attache un listener pour détecter le scroll manuel et la position
 * @param {HTMLElement} element - L'élément à surveiller
 * @param {object} dotNetHelper - Référence .NET pour les callbacks
 */
export function attachScrollListener(element, dotNetHelper) {
  if (!element) {
    return;
  }

  let scrollTimeout;
  let lastCollapsedState = false;

  element.addEventListener('scroll', function () {
    clearTimeout(scrollTimeout);

    scrollTimeout = setTimeout(() => {
      const scrollTop = element.scrollTop;
      const scrollHeight = element.scrollHeight;
      const clientHeight = element.clientHeight;

      // Vérifier si on est en bas (pour auto-scroll)
      const isAtBottom = Math.abs(
        scrollHeight - clientHeight - scrollTop
      ) < 10;

      if (dotNetHelper) {
        dotNetHelper.invokeMethodAsync('OnUserScroll', isAtBottom);
      }

      // Calculer si le header doit être collapsé
      // Si on a scrollé de plus de 50px depuis le haut, on collapse
      const shouldCollapse = scrollTop > 50;

      // Ne notifier que si l'état a changé
      if (shouldCollapse !== lastCollapsedState) {
        lastCollapsedState = shouldCollapse;
        if (dotNetHelper) {
          dotNetHelper.invokeMethodAsync('OnScrollPositionChanged', shouldCollapse);
        }
      }
    }, 100); // Throttle à 100ms
  });
}

/**
 * Récupère la position de scroll actuelle
 * @param {HTMLElement} element - L'élément
 * @returns {object} - Position et dimensions
 */
export function getScrollPosition(element) {
  if (!element) {
    return { top: 0, height: 0, scrollHeight: 0 };
  }

  return {
    top: element.scrollTop,
    height: element.clientHeight,
    scrollHeight: element.scrollHeight
  };
}
