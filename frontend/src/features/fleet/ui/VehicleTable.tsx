"use client";

import { formatStationaryFor } from "../domain/vehiclePresentation";
import type { VehicleSnapshot } from "../domain/types";
import { StatusChip } from "./StatusChip";

interface Props {
  vehicles: readonly VehicleSnapshot[];
  selectedId: string | null;
  onSelect: (vehicleId: string) => void;
}

export function VehicleTable({ vehicles, selectedId, onSelect }: Props) {
  return (
    <div className="overflow-x-auto">
      <table className="w-full border-collapse text-sm">
        <thead>
          <tr className="border-b border-slate-800 text-left text-xs uppercase tracking-wide text-slate-500">
            <th className="px-4 py-2 font-medium">Vehículo</th>
            <th className="px-4 py-2 font-medium">Estado</th>
            <th className="px-4 py-2 font-medium">Parado desde</th>
            <th className="px-4 py-2 font-medium">Última señal</th>
          </tr>
        </thead>
        <tbody>
          {vehicles.map((vehicle) => (
            <tr
              key={vehicle.vehicleId}
              onClick={() => onSelect(vehicle.vehicleId)}
              className={`cursor-pointer border-b border-slate-900 transition-colors hover:bg-slate-800/50 ${
                selectedId === vehicle.vehicleId ? "bg-slate-800/70" : ""
              }`}
            >
              <td className="px-4 py-2.5 font-mono text-slate-200">{vehicle.label}</td>
              <td className="px-4 py-2.5">
                <StatusChip status={vehicle.status} />
              </td>
              <td className="px-4 py-2.5 tabular-nums text-slate-400">
                {formatStationaryFor(vehicle.stationarySince)}
              </td>
              <td className="px-4 py-2.5 tabular-nums text-slate-400">
                {vehicle.lastSeenAt
                  ? new Date(vehicle.lastSeenAt).toLocaleTimeString("es-CO")
                  : "—"}
              </td>
            </tr>
          ))}

          {vehicles.length === 0 && (
            <tr>
              <td colSpan={4} className="px-4 py-8 text-center text-slate-500">
                Sin vehículos. Comprueba que el simulador esté en marcha.
              </td>
            </tr>
          )}
        </tbody>
      </table>
    </div>
  );
}
