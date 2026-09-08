"use client";

import dynamic from "next/dynamic";
import type { TrackPoint, VehicleSnapshot } from "../domain/types";

/**
 * Leaflet toca `window` en el momento de importarse, así que el módulo no puede evaluarse durante
 * el render en servidor. Se carga solo en cliente.
 */
const FleetMap = dynamic(() => import("./FleetMap"), {
  ssr: false,
  loading: () => (
    <div className="flex size-full items-center justify-center text-sm text-slate-500">
      Cargando mapa…
    </div>
  ),
});

interface Props {
  vehicles: readonly VehicleSnapshot[];
  track: readonly TrackPoint[];
  selectedId: string | null;
  onSelect: (vehicleId: string) => void;
}

export function FleetMapPanel(props: Props) {
  return <FleetMap {...props} />;
}
