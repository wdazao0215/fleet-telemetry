import { describe, expect, it } from "vitest";
import {
  applyUpdate,
  byOperationalPriority,
  formatStationaryFor,
} from "../domain/vehiclePresentation";
import type { VehicleSnapshot, VehicleStateUpdate } from "../domain/types";

function vehicle(overrides: Partial<VehicleSnapshot> & { vehicleId: string }): VehicleSnapshot {
  return {
    label: overrides.vehicleId,
    status: "Moving",
    lifecycleState: "Active",
    latitude: 4.71,
    longitude: -74.07,
    lastSeenAt: "2026-01-01T10:00:00Z",
    stationarySince: null,
    hasUnacknowledgedAlert: false,
    ...overrides,
  };
}

describe("byOperationalPriority", () => {
  it("pone delante lo que exige atención, no el orden alfabético", () => {
    // Un operador no quiere una lista alfabética: quiere ver primero lo que está mal.
    const fleet = [
      vehicle({ vehicleId: "VH-001", status: "Offline" }),
      vehicle({ vehicleId: "VH-002", status: "Moving" }),
      vehicle({ vehicleId: "VH-003", status: "Alerted" }),
      vehicle({ vehicleId: "VH-004", status: "Stopped" }),
    ];

    const ordered = [...fleet].sort(byOperationalPriority).map((v) => v.status);

    expect(ordered).toEqual(["Alerted", "Stopped", "Moving", "Offline"]);
  });

  it("mantiene un orden estable dentro del mismo estado", () => {
    // Sin desempate por identificador, la tabla bailaría en cada refresco y sería ilegible.
    const fleet = [
      vehicle({ vehicleId: "VH-009", status: "Moving" }),
      vehicle({ vehicleId: "VH-002", status: "Moving" }),
    ];

    expect([...fleet].sort(byOperationalPriority).map((v) => v.vehicleId)).toEqual([
      "VH-002",
      "VH-009",
    ]);
  });
});

describe("applyUpdate", () => {
  const update: VehicleStateUpdate = {
    vehicleId: "VH-001",
    latitude: 4.72,
    longitude: -74.08,
    recordedAt: "2026-01-01T10:05:00Z",
    status: "Stopped",
    stationarySince: "2026-01-01T10:03:00Z",
  };

  it("actualiza el vehículo existente sin duplicarlo", () => {
    const result = applyUpdate([vehicle({ vehicleId: "VH-001" })], update);

    expect(result).toHaveLength(1);
    expect(result[0].status).toBe("Stopped");
    expect(result[0].latitude).toBe(4.72);
  });

  it("añade un vehículo que emite por primera vez", () => {
    // En campo, un dispositivo nuevo empieza a reportar sin que nadie lo diera de alta antes; debe
    // aparecer sin esperar al siguiente refresco completo.
    const result = applyUpdate([vehicle({ vehicleId: "VH-002" })], update);

    expect(result).toHaveLength(2);
    expect(result.map((v) => v.vehicleId)).toContain("VH-001");
  });

  it("conserva la etiqueta y el estado de ciclo de vida que no vienen en la actualización", () => {
    const result = applyUpdate(
      [vehicle({ vehicleId: "VH-001", label: "Camión 1", lifecycleState: "PendingDeletion" })],
      update,
    );

    expect(result[0].label).toBe("Camión 1");
    expect(result[0].lifecycleState).toBe("PendingDeletion");
  });
});

describe("formatStationaryFor", () => {
  const now = new Date("2026-01-01T10:00:00Z");

  it("muestra un guion cuando el vehículo se está moviendo", () => {
    expect(formatStationaryFor(null, now)).toBe("—");
  });

  it("usa segundos por debajo del minuto", () => {
    expect(formatStationaryFor("2026-01-01T09:59:30Z", now)).toBe("30 s");
  });

  it("usa minutos y horas cuando corresponde", () => {
    expect(formatStationaryFor("2026-01-01T09:45:00Z", now)).toBe("15 min");
    expect(formatStationaryFor("2026-01-01T08:30:00Z", now)).toBe("1 h 30 min");
  });

  it("no muestra tiempos negativos con un reloj desfasado", () => {
    expect(formatStationaryFor("2026-01-01T10:05:00Z", now)).toBe("0 s");
  });
});
