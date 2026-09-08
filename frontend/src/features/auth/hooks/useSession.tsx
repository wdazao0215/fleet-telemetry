"use client";

import { createContext, useCallback, useContext, useEffect, useMemo, useState } from "react";
import { requestToken } from "../api/auth.client";
import { isExpired, type Session } from "../domain/types";

const STORAGE_KEY = "fleet.session";

interface SessionContextValue {
  session: Session | null;
  isReady: boolean;
  signIn: (username: string, password: string) => Promise<void>;
  signOut: () => void;
}

const SessionContext = createContext<SessionContextValue | null>(null);

/**
 * Sesión del operador.
 *
 * El token se guarda en sessionStorage, no en localStorage: así muere al cerrar la pestaña y no
 * queda un JWT válido durante horas en un ordenador compartido de una sala de control. Sigue siendo
 * vulnerable a XSS; la solución correcta es una cookie httpOnly emitida por el backend, y está
 * anotada en el README como trabajo pendiente para producción.
 */
export function SessionProvider({ children }: { children: React.ReactNode }) {
  const [session, setSession] = useState<Session | null>(null);
  const [isReady, setIsReady] = useState(false);

  useEffect(() => {
    // Solo en el cliente: sessionStorage no existe durante el render en servidor.
    const stored = window.sessionStorage.getItem(STORAGE_KEY);

    if (stored) {
      try {
        const parsed = JSON.parse(stored) as Session;
        if (!isExpired(parsed)) {
          setSession(parsed);
        } else {
          window.sessionStorage.removeItem(STORAGE_KEY);
        }
      } catch {
        window.sessionStorage.removeItem(STORAGE_KEY);
      }
    }

    setIsReady(true);
  }, []);

  const signIn = useCallback(async (username: string, password: string) => {
    const issued = await requestToken(username, password);
    window.sessionStorage.setItem(STORAGE_KEY, JSON.stringify(issued));
    setSession(issued);
  }, []);

  const signOut = useCallback(() => {
    window.sessionStorage.removeItem(STORAGE_KEY);
    setSession(null);
  }, []);

  const value = useMemo(
    () => ({ session, isReady, signIn, signOut }),
    [session, isReady, signIn, signOut],
  );

  return <SessionContext value={value}>{children}</SessionContext>;
}

export function useSession(): SessionContextValue {
  const context = useContext(SessionContext);

  if (!context) {
    throw new Error("useSession debe usarse dentro de SessionProvider.");
  }

  return context;
}
