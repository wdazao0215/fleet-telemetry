import { describe, expect, it } from "vitest";
import {
  MAX_ATTEMPTS,
  retryDelayMs,
  selectBatch,
  selectExhausted,
} from "../domain/outboxPolicy";
import type { QueuedReading } from "../domain/types";

function reading(overrides: Partial<QueuedReading> & { id: string }): QueuedReading {
  return {
    vehicleId: "VH-001",
    latitude: 4.71,
    longitude: -74.07,
    timestamp: "2026-01-01T10:00:00.000Z",
    attempts: 0,
    ...overrides,
  };
}

describe("selectBatch", () => {
  it("agrupa las lecturas en un solo lote acotado", () => {
    // El caso del túnel: diez minutos sin cobertura son ~200 lecturas. Enviarlas de una en una
    // sería una avalancha por cada conductor que sale del túnel a la vez.
    const queued = Array.from({ length: 200 }, (_, index) =>
      reading({ id: `r${index}`, timestamp: `2026-01-01T10:${String(index % 60).padStart(2, "0")}:00.000Z` }),
    );

    expect(selectBatch(queued)).toHaveLength(50);
  });

  it("envía primero lo más antiguo", () => {
    // El orden importa: el backend calcula la detención comparando lecturas consecutivas.
    const queued = [
      reading({ id: "nueva", timestamp: "2026-01-01T10:05:00.000Z" }),
      reading({ id: "vieja", timestamp: "2026-01-01T10:00:00.000Z" }),
    ];

    expect(selectBatch(queued).map((r) => r.id)).toEqual(["vieja", "nueva"]);
  });

  it("no reintenta indefinidamente una lectura que siempre falla", () => {
    // Sin tope, una lectura corrupta bloquearía la cola y el conductor dejaría de reportar.
    const queued = [
      reading({ id: "atascada", attempts: MAX_ATTEMPTS }),
      reading({ id: "sana", attempts: 1 }),
    ];

    expect(selectBatch(queued).map((r) => r.id)).toEqual(["sana"]);
    expect(selectExhausted(queued).map((r) => r.id)).toEqual(["atascada"]);
  });
});

describe("retryDelayMs", () => {
  it("crece exponencialmente para no gastar batería dentro de un túnel", () => {
    expect(retryDelayMs(1)).toBe(2_000);
    expect(retryDelayMs(2)).toBe(4_000);
    expect(retryDelayMs(3)).toBe(8_000);
  });

  it("se detiene en un minuto para no tardar horas en recuperarse", () => {
    expect(retryDelayMs(20)).toBe(60_000);
  });
});
