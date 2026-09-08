export interface Alert {
  readonly id: string;
  readonly vehicleId: string;
  readonly kind: "StoppedVehicle" | "PanicButton";
  readonly latitude: number;
  readonly longitude: number;
  readonly raisedAt: string;
  readonly acknowledgedAt: string | null;
  readonly detail: string;
}

export const ALERT_LABELS: Record<Alert["kind"], string> = {
  StoppedVehicle: "Vehículo detenido",
  PanicButton: "Botón de pánico",
};
