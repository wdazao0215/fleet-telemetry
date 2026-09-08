"use client";

import { useEffect, useMemo } from "react";
import { MapContainer, Marker, Polyline, Popup, TileLayer, useMap } from "react-leaflet";
import L from "leaflet";
import "leaflet/dist/leaflet.css";
import { STATUS_PRESENTATION } from "../domain/vehiclePresentation";
import type { TrackPoint, VehicleSnapshot, VehicleStatus } from "../domain/types";

/** Centro por defecto: Bogotá, que es donde el simulador genera las rutas. */
const DEFAULT_CENTER: [number, number] = [4.6533, -74.0836];

const STATUS_COLORS: Record<VehicleStatus, string> = {
  Alerted: "#ef4444",
  Stopped: "#fbbf24",
  Moving: "#34d399",
  Offline: "#64748b",
};

/**
 * Marcador dibujado con CSS en lugar de la imagen por defecto de Leaflet.
 *
 * Evita el problema clásico de los iconos rotos con bundlers —Leaflet resuelve sus PNG por ruta
 * relativa y Next los sirve con hash— y, de paso, deja que el color transmita el estado del
 * vehículo sin cargar cuatro imágenes distintas.
 */
function markerFor(status: VehicleStatus, isSelected: boolean): L.DivIcon {
  const size = isSelected ? 18 : 14;
  const color = STATUS_COLORS[status];

  return L.divIcon({
    className: "",
    html: `<span style="
      display:block;width:${size}px;height:${size}px;border-radius:9999px;
      background:${color};
      box-shadow:0 0 0 ${isSelected ? 4 : 2}px rgba(255,255,255,.25), 0 1px 4px rgba(0,0,0,.6);
    "></span>`,
    iconSize: [size, size],
    iconAnchor: [size / 2, size / 2],
  });
}

/** Centra el mapa cuando el operador selecciona un vehículo en la tabla. */
function RecenterOnSelection({ position }: { position: [number, number] | null }) {
  const map = useMap();

  useEffect(() => {
    if (position) {
      map.flyTo(position, Math.max(map.getZoom(), 14), { duration: 0.6 });
    }
  }, [map, position]);

  return null;
}

interface Props {
  vehicles: readonly VehicleSnapshot[];
  track: readonly TrackPoint[];
  selectedId: string | null;
  onSelect: (vehicleId: string) => void;
}

export default function FleetMap({ vehicles, track, selectedId, onSelect }: Props) {
  const located = useMemo(
    () =>
      vehicles.filter(
        (vehicle): vehicle is VehicleSnapshot & { latitude: number; longitude: number } =>
          vehicle.latitude !== null && vehicle.longitude !== null,
      ),
    [vehicles],
  );

  const selectedPosition = useMemo(() => {
    const selected = located.find((vehicle) => vehicle.vehicleId === selectedId);
    return selected ? ([selected.latitude, selected.longitude] as [number, number]) : null;
  }, [located, selectedId]);

  const trackLine = useMemo(
    () => track.map((point) => [point.latitude, point.longitude] as [number, number]),
    [track],
  );

  return (
    <MapContainer
      center={DEFAULT_CENTER}
      zoom={12}
      className="size-full"
      // El mapa vive dentro de un panel con su propio scroll; el zoom con rueda robaría el scroll
      // de la página al pasar por encima.
      scrollWheelZoom={false}
    >
      <TileLayer
        // Teselas de OpenStreetMap: gratuitas y sin registro, que es lo que pide el enunciado. Se
        // descartó el estilo oscuro de CARTO porque ahora exige API key y estampa "API KEY
        // REQUIRED" sobre el mapa. El aspecto oscuro se consigue con un filtro CSS sobre el panel
        // de teselas, que no afecta a los marcadores porque Leaflet los pinta en otro panel.
        url="https://tile.openstreetmap.org/{z}/{x}/{y}.png"
        attribution='&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a>'
        maxZoom={19}
      />

      {trackLine.length > 1 && (
        <Polyline positions={trackLine} pathOptions={{ color: "#38bdf8", weight: 3, opacity: 0.7 }} />
      )}

      {located.map((vehicle) => (
        <Marker
          key={vehicle.vehicleId}
          position={[vehicle.latitude, vehicle.longitude]}
          icon={markerFor(vehicle.status, vehicle.vehicleId === selectedId)}
          eventHandlers={{ click: () => onSelect(vehicle.vehicleId) }}
        >
          <Popup>
            <span className="font-mono font-semibold">{vehicle.label}</span>
            <br />
            {STATUS_PRESENTATION[vehicle.status].label}
          </Popup>
        </Marker>
      ))}

      <RecenterOnSelection position={selectedPosition} />
    </MapContainer>
  );
}
