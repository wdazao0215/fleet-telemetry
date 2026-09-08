"use client";

import { useEffect } from "react";

/**
 * Registra el service worker del panel del conductor.
 *
 * Solo se monta en /driver: el dashboard de la sala de control se usa con red y cachearlo solo
 * añadiría una capa más donde depurar cuando algo no se actualiza.
 */
export function ServiceWorkerRegistration() {
  useEffect(() => {
    if (!("serviceWorker" in navigator)) {
      return;
    }

    // El registro se pospone al evento load: competir con la carga inicial retrasaría el primer
    // pintado justo en el dispositivo más lento del sistema.
    const register = () => {
      void navigator.serviceWorker.register("/sw.js", { scope: "/" }).catch(() => {
        // Sin service worker la app sigue funcionando: pierde el arranque offline, no la cola.
      });
    };

    if (document.readyState === "complete") {
      register();
    } else {
      window.addEventListener("load", register);
      return () => window.removeEventListener("load", register);
    }
  }, []);

  return null;
}
