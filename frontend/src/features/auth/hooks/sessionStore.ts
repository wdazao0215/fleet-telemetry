import { isExpired, type Session } from "../domain/types";

const STORAGE_KEY = "fleet.session";

/**
 * Store externo de la sesión, sobre sessionStorage.
 *
 * Se modela como store con suscripción para poder leerlo con `useSyncExternalStore`, que es la API
 * pensada para exactamente esto: leer de una fuente externa al árbol de React sin romper el render
 * en servidor ni disparar setState dentro de un efecto.
 *
 * sessionStorage y no localStorage: el token muere al cerrar la pestaña y no queda un JWT válido
 * durante horas en un equipo compartido de una sala de control. Sigue siendo vulnerable a XSS; la
 * solución de producción es una cookie httpOnly emitida por el backend, anotada en el README.
 */
const listeners = new Set<() => void>();

let cached: Session | null = null;
let cachedRaw: string | null = null;

function read(): Session | null {
  if (typeof window === "undefined") {
    return null;
  }

  const raw = window.sessionStorage.getItem(STORAGE_KEY);

  if (raw === null) {
    cachedRaw = null;
    cached = null;
    return null;
  }

  // useSyncExternalStore exige que getSnapshot devuelva el mismo objeto mientras nada cambie; sin
  // esta caché, cada render crearía uno nuevo y React entraría en un bucle infinito.
  if (raw === cachedRaw) {
    return cached;
  }

  cachedRaw = raw;

  try {
    const parsed = JSON.parse(raw) as Session;

    if (isExpired(parsed)) {
      window.sessionStorage.removeItem(STORAGE_KEY);
      cachedRaw = null;
      cached = null;
      return null;
    }

    cached = parsed;
  } catch {
    window.sessionStorage.removeItem(STORAGE_KEY);
    cachedRaw = null;
    cached = null;
  }

  return cached;
}

function emit(): void {
  for (const listener of listeners) {
    listener();
  }
}

export const sessionStore = {
  subscribe(listener: () => void): () => void {
    listeners.add(listener);

    // 'storage' avisa de los cambios hechos en otra pestaña: cerrar sesión en una debe cerrarla en
    // todas, no dejar una pestaña olvidada con el panel abierto.
    window.addEventListener("storage", emit);

    return () => {
      listeners.delete(listener);

      if (listeners.size === 0) {
        window.removeEventListener("storage", emit);
      }
    };
  },

  getSnapshot: read,

  /** En el servidor no hay sesión; React re-renderiza en cliente tras hidratar. */
  getServerSnapshot: (): Session | null => null,

  save(session: Session): void {
    window.sessionStorage.setItem(STORAGE_KEY, JSON.stringify(session));
    emit();
  },

  clear(): void {
    window.sessionStorage.removeItem(STORAGE_KEY);
    emit();
  },
};
