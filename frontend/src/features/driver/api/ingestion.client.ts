import { config } from "@/lib/config";
import type { QueuedReading } from "../domain/types";

export interface SendOutcome {
  /** Lecturas que el servidor aceptó o rechazó definitivamente: se pueden borrar de la cola. */
  readonly settled: string[];
  /** Lecturas que deben reintentarse: fallo de red o error del servidor. */
  readonly retry: string[];
}

/**
 * Envía un lote de lecturas a la API de ingesta.
 *
 * Cada lectura va en su propia petición porque el endpoint del enunciado recibe una posición por
 * llamada. El envío se paraleliza en el lote, y lo que decide qué se borra de la cola no es "hubo
 * error" sino **si el error tiene arreglo**: un 400 significa que esa lectura no va a ser aceptada
 * nunca, así que reintentarla eternamente solo consumiría batería y datos.
 */
export async function sendBatch(readings: readonly QueuedReading[]): Promise<SendOutcome> {
  const settled: string[] = [];
  const retry: string[] = [];

  await Promise.all(
    readings.map(async (reading) => {
      try {
        const response = await fetch(`${config.ingestionApiUrl}/api/v1/telemetry`, {
          method: "POST",
          headers: {
            "Content-Type": "application/json",
            "X-Api-Key": config.ingestionApiKey,
            // Permite seguir la lectura desde el móvil hasta el dashboard con un solo identificador.
            "X-Correlation-Id": reading.id,
          },
          body: JSON.stringify({
            vehicleId: reading.vehicleId,
            latitude: reading.latitude,
            longitude: reading.longitude,
            timestamp: reading.timestamp,
          }),
        });

        if (response.ok || response.status === 400) {
          settled.push(reading.id);
        } else {
          retry.push(reading.id);
        }
      } catch {
        // Sin red: la lectura se queda en la cola tal cual.
        retry.push(reading.id);
      }
    }),
  );

  return { settled, retry };
}
