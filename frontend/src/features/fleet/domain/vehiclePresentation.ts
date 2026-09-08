import type { VehicleSnapshot, VehicleStateUpdate, VehicleStatus } from "./types";

/**
 * Presentación de cada estado.
 *
 * Cada estado lleva color **y** etiqueta de texto. El color por sí solo no distingue nada para una
 * persona con daltopía, y un panel de flota se mira bajo presión: el texto es lo que se lee cuando
 * hay prisa.
 */
export const STATUS_PRESENTATION: Record<
  VehicleStatus,
  { label: string; dot: string; chip: string; order: number }
> = {
  Alerted: {
    label: "Alerta",
    dot: "bg-red-500",
    chip: "bg-red-500/15 text-red-300 ring-red-500/30",
    order: 0,
  },
  Stopped: {
    label: "Detenido",
    dot: "bg-amber-400",
    chip: "bg-amber-400/15 text-amber-200 ring-amber-400/30",
    order: 1,
  },
  Moving: {
    label: "En movimiento",
    dot: "bg-emerald-400",
    chip: "bg-emerald-400/15 text-emerald-200 ring-emerald-400/30",
    order: 2,
  },
  Offline: {
    label: "Sin señal",
    dot: "bg-slate-500",
    chip: "bg-slate-500/15 text-slate-300 ring-slate-500/30",
    order: 3,
  },
};

/**
 * Ordena la flota poniendo delante lo que exige atención.
 *
 * Un operador no quiere una lista alfabética: quiere ver primero lo que está mal. Dentro de cada
 * estado se ordena por identificador para que la tabla no baile entre refrescos.
 */
export function byOperationalPriority(a: VehicleSnapshot, b: VehicleSnapshot): number {
  const difference = STATUS_PRESENTATION[a.status].order - STATUS_PRESENTATION[b.status].order;
  return difference !== 0 ? difference : a.vehicleId.localeCompare(b.vehicleId);
}

/** Aplica una actualización en vivo sobre el snapshot que ya está en pantalla. */
export function applyUpdate(
  vehicles: readonly VehicleSnapshot[],
  update: VehicleStateUpdate,
): VehicleSnapshot[] {
  const index = vehicles.findIndex((vehicle) => vehicle.vehicleId === update.vehicleId);

  const updated: VehicleSnapshot = {
    ...(index >= 0
      ? vehicles[index]
      : {
          vehicleId: update.vehicleId,
          label: update.vehicleId,
          lifecycleState: "Active" as const,
          hasUnacknowledgedAlert: false,
        }),
    status: update.status,
    latitude: update.latitude,
    longitude: update.longitude,
    lastSeenAt: update.recordedAt,
    stationarySince: update.stationarySince,
  };

  // Un vehículo que emite por primera vez debe aparecer sin esperar al siguiente refresco completo:
  // en campo, un dispositivo nuevo empieza a reportar sin que nadie lo diera de alta antes.
  if (index < 0) {
    return [...vehicles, updated];
  }

  const next = [...vehicles];
  next[index] = updated;
  return next;
}

/** Cuánto lleva parado, en formato corto para la tabla. */
export function formatStationaryFor(stationarySince: string | null, now: Date = new Date()): string {
  if (!stationarySince) {
    return "—";
  }

  const seconds = Math.max(0, Math.floor((now.getTime() - new Date(stationarySince).getTime()) / 1000));

  if (seconds < 60) {
    return `${seconds} s`;
  }

  const minutes = Math.floor(seconds / 60);
  return minutes < 60 ? `${minutes} min` : `${Math.floor(minutes / 60)} h ${minutes % 60} min`;
}
