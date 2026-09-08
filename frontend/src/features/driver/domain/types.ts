/** Lectura tomada por el dispositivo del conductor, pendiente o ya enviada. */
export interface QueuedReading {
  /** Clave local; también sirve de idempotencia si el envío se reintenta. */
  readonly id: string;
  readonly vehicleId: string;
  readonly latitude: number;
  readonly longitude: number;
  readonly timestamp: string;
  readonly attempts: number;
}

export interface LocalAlert {
  readonly id: string;
  readonly kind: "panic" | "connectionLost" | "connectionRestored";
  readonly message: string;
  readonly at: string;
}

export type LinkState = "online" | "offline" | "syncing";
