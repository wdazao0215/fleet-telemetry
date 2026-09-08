export const VEHICLE_STATUSES = ["Moving", "Stopped", "Alerted", "Offline"] as const;

export type VehicleStatus = (typeof VEHICLE_STATUSES)[number];

export type LifecycleState = "Active" | "PendingDeletion" | "DeletionFailed" | "Deleted";

export interface VehicleSnapshot {
  readonly vehicleId: string;
  readonly label: string;
  readonly status: VehicleStatus;
  readonly lifecycleState: LifecycleState;
  readonly latitude: number | null;
  readonly longitude: number | null;
  readonly lastSeenAt: string | null;
  readonly stationarySince: string | null;
  readonly hasUnacknowledgedAlert: boolean;
}

export interface TrackPoint {
  readonly latitude: number;
  readonly longitude: number;
  readonly recordedAt: string;
}

/** Actualización en vivo que llega por SignalR cuando el worker procesa una posición. */
export interface VehicleStateUpdate {
  readonly vehicleId: string;
  readonly latitude: number;
  readonly longitude: number;
  readonly recordedAt: string;
  readonly status: VehicleStatus;
  readonly stationarySince: string | null;
}
