export interface Session {
  readonly accessToken: string;
  readonly expiresAt: string;
}

/**
 * Un token a punto de caducar se trata como caducado.
 *
 * El margen evita el caso molesto de renovar justo cuando una petición ya está en vuelo y recibir
 * un 401 por unos segundos de diferencia.
 */
export function isExpired(session: Session, now: Date = new Date()): boolean {
  const EXPIRY_MARGIN_MS = 30_000;
  return new Date(session.expiresAt).getTime() - EXPIRY_MARGIN_MS <= now.getTime();
}
