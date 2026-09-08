"use client";

import { createContext, useCallback, useContext, useMemo, useSyncExternalStore } from "react";
import { requestToken } from "../api/auth.client";
import type { Session } from "../domain/types";
import { sessionStore } from "./sessionStore";

interface SessionContextValue {
  session: Session | null;
  isReady: boolean;
  signIn: (username: string, password: string) => Promise<void>;
  signOut: () => void;
}

const SessionContext = createContext<SessionContextValue | null>(null);

export function SessionProvider({ children }: { children: React.ReactNode }) {
  // useSyncExternalStore lee sessionStorage sin efectos ni setState: el valor del servidor es null
  // y React re-renderiza con el real al hidratar, sin desajuste de hidratación.
  const session = useSyncExternalStore(
    sessionStore.subscribe,
    sessionStore.getSnapshot,
    sessionStore.getServerSnapshot,
  );

  // Durante el render en servidor no se sabe todavía si hay sesión; sin esta distinción, el
  // formulario de login parpadearía un instante para un operador que ya había entrado.
  const isReady = typeof window !== "undefined";

  const signIn = useCallback(async (username: string, password: string) => {
    sessionStore.save(await requestToken(username, password));
  }, []);

  const signOut = useCallback(() => sessionStore.clear(), []);

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
