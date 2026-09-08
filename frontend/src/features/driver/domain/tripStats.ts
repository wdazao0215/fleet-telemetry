import type { QueuedReading } from "./types";

const EARTH_RADIUS_METERS = 6_371_008.8;

/**
 * Distancia entre dos puntos, con la misma fórmula de Haversine que usa el backend.
 *
 * Se repite aquí a propósito y no se pide al servidor: el objetivo de la app del conductor es
 * funcionar sin red. Un contador de kilómetros que se queda en blanco dentro de un túnel sería
 * exactamente el fallo que este cliente tiene que evitar.
 */
export function distanceInMeters(
  a: { latitude: number; longitude: number },
  b: { latitude: number; longitude: number },
): number {
  const toRadians = (degrees: number) => (degrees * Math.PI) / 180;

  const deltaLatitude = toRadians(b.latitude - a.latitude);
  const deltaLongitude = toRadians(b.longitude - a.longitude);

  const h =
    Math.sin(deltaLatitude / 2) ** 2 +
    Math.cos(toRadians(a.latitude)) *
      Math.cos(toRadians(b.latitude)) *
      Math.sin(deltaLongitude / 2) ** 2;

  return 2 * EARTH_RADIUS_METERS * Math.asin(Math.min(1, Math.sqrt(h)));
}

export interface TripStats {
  readonly distanceMeters: number;
  readonly points: number;
}

/** Distancia acumulada del viaje actual. */
export function tripStats(readings: readonly QueuedReading[]): TripStats {
  let distance = 0;

  for (let index = 1; index < readings.length; index++) {
    const step = distanceInMeters(readings[index - 1], readings[index]);

    // Se descartan los saltos imposibles. Un GPS urbano da ocasionalmente una lectura disparada por
    // reflexión en edificios, y sumarla inflaría el viaje en kilómetros que nunca se recorrieron.
    if (step < 2_000) {
      distance += step;
    }
  }

  return { distanceMeters: distance, points: readings.length };
}

export function formatDistance(meters: number): string {
  return meters < 1_000 ? `${Math.round(meters)} m` : `${(meters / 1000).toFixed(2)} km`;
}
