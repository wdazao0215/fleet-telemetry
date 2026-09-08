import type { QueuedReading } from "./types";

/**
 * Cuántas lecturas se envían juntas al recuperar la conexión.
 *
 * El enunciado plantea el caso del túnel: diez minutos sin cobertura son unas 200 lecturas. Enviarlas
 * de una en una serían 200 peticiones de golpe por cada conductor que sale del túnel a la vez, que
 * es precisamente cómo se satura un servidor justo cuando vuelve la red.
 */
export const MAX_BATCH_SIZE = 50;

/**
 * Descartes tras los que se abandona una lectura.
 *
 * Sin tope, una lectura que el servidor rechaza siempre —un payload que quedó corrupto en disco—
 * bloquearía la cola para siempre y el conductor no volvería a reportar.
 */
export const MAX_ATTEMPTS = 5;

/** Lecturas que toca enviar ahora, en orden cronológico. */
export function selectBatch(
  readings: readonly QueuedReading[],
  maxBatchSize: number = MAX_BATCH_SIZE,
): QueuedReading[] {
  return [...readings]
    .filter((reading) => reading.attempts < MAX_ATTEMPTS)
    .sort((a, b) => a.timestamp.localeCompare(b.timestamp))
    .slice(0, maxBatchSize);
}

/** Lecturas que hay que abandonar por haber agotado los reintentos. */
export function selectExhausted(readings: readonly QueuedReading[]): QueuedReading[] {
  return readings.filter((reading) => reading.attempts >= MAX_ATTEMPTS);
}

/**
 * Espera antes del siguiente intento, con backoff exponencial acotado.
 *
 * Reintentar cada segundo dentro de un túnel gasta batería sin ninguna posibilidad de éxito; el
 * tope evita que, tras una avería larga, la app tarde horas en volver a intentarlo.
 */
export function retryDelayMs(consecutiveFailures: number): number {
  const BASE_MS = 2_000;
  const MAX_MS = 60_000;

  return Math.min(MAX_MS, BASE_MS * 2 ** Math.max(0, consecutiveFailures - 1));
}
