import { describe, expect, it } from "vitest";
import { distanceInMeters, formatDistance, tripStats } from "../domain/tripStats";
import type { QueuedReading } from "../domain/types";

function at(latitude: number, longitude: number, id: string): QueuedReading {
  return { id, vehicleId: "VH-001", latitude, longitude, timestamp: "2026-01-01T10:00:00Z", attempts: 0 };
}

describe("distanceInMeters", () => {
  it("coincide con el valor de referencia para un grado de longitud en Bogotá", () => {
    expect(distanceInMeters({ latitude: 4.5981, longitude: -74.0758 }, { latitude: 4.5981, longitude: -73.0758 }))
      .toBeCloseTo(110_950, -3);
  });

  it("es cero para el mismo punto", () => {
    expect(distanceInMeters({ latitude: 4.71, longitude: -74.07 }, { latitude: 4.71, longitude: -74.07 })).toBe(0);
  });
});

describe("tripStats", () => {
  it("acumula la distancia entre puntos consecutivos", () => {
    const trip = tripStats([at(4.7100, -74.0700, "a"), at(4.7110, -74.0700, "b")]);

    expect(trip.points).toBe(2);
    expect(trip.distanceMeters).toBeGreaterThan(100);
    expect(trip.distanceMeters).toBeLessThan(130);
  });

  it("descarta el salto imposible de una lectura GPS disparada", () => {
    // Un rebote en un edificio puede devolver una posición a cientos de kilómetros. Sumarla
    // inflaría el viaje con kilómetros que nunca se recorrieron.
    const trip = tripStats([at(4.7100, -74.0700, "a"), at(9.0000, -70.0000, "salto"), at(4.7110, -74.0700, "b")]);

    expect(trip.distanceMeters).toBe(0);
  });
});

describe("formatDistance", () => {
  it("usa metros por debajo del kilómetro y kilómetros por encima", () => {
    expect(formatDistance(450)).toBe("450 m");
    expect(formatDistance(1_500)).toBe("1.50 km");
  });
});
