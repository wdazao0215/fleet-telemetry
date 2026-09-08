"use client";

import { useEffect, useMemo, useState } from "react";
import { useSession } from "@/features/auth/hooks/useSession";
import { LoginForm } from "@/features/auth/ui/LoginForm";
import { useFleetLive } from "@/features/fleet/hooks/useFleetLive";
import { byOperationalPriority } from "@/features/fleet/domain/vehiclePresentation";
import { fetchTrack } from "@/features/fleet/api/fleet.client";
import type { TrackPoint } from "@/features/fleet/domain/types";
import { VehicleTable } from "@/features/fleet/ui/VehicleTable";
import { FleetMapPanel } from "@/features/fleet/ui/FleetMapPanel";
import { TransportIndicator } from "@/features/fleet/ui/TransportIndicator";
import { AlertPanel } from "@/features/alerts/ui/AlertPanel";

export default function DashboardPage() {
  const { session, isReady, signOut } = useSession();
  const token = session?.accessToken ?? null;
  const { vehicles, alerts, transport, error } = useFleetLive(token);

  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [track, setTrack] = useState<TrackPoint[]>([]);

  const ordered = useMemo(() => [...vehicles].sort(byOperationalPriority), [vehicles]);

  useEffect(() => {
    if (!token || !selectedId) {
      setTrack([]);
      return;
    }

    const controller = new AbortController();

    fetchTrack(token, selectedId, controller.signal)
      .then(setTrack)
      .catch(() => {
        // El recorrido es información complementaria: si falla, el mapa sigue mostrando posiciones
        // actuales en lugar de dejar la pantalla en un estado de error.
      });

    return () => controller.abort();
  }, [token, selectedId]);

  if (!isReady) {
    return null;
  }

  if (!session) {
    return (
      <main className="flex min-h-dvh items-center justify-center p-6">
        <LoginForm />
      </main>
    );
  }

  const alerted = ordered.filter((vehicle) => vehicle.status === "Alerted").length;

  return (
    <main className="flex min-h-dvh flex-col">
      <header className="flex flex-wrap items-center justify-between gap-3 border-b border-slate-800 px-5 py-3">
        <div className="flex items-baseline gap-3">
          <h1 className="text-base font-semibold">Fleet Telemetry</h1>
          <span className="text-xs text-slate-500">
            {ordered.length} vehículos · {alerted} en alerta
          </span>
        </div>

        <div className="flex items-center gap-3">
          <TransportIndicator transport={transport} />
          <button
            onClick={signOut}
            className="rounded-lg border border-slate-700 px-2.5 py-1 text-xs text-slate-300 transition-colors hover:bg-slate-800"
          >
            Salir
          </button>
        </div>
      </header>

      {error && (
        <p role="alert" className="border-b border-red-900/50 bg-red-950/40 px-5 py-2 text-sm text-red-300">
          {error}
        </p>
      )}

      <div className="grid flex-1 gap-px bg-slate-800 lg:grid-cols-[minmax(0,1fr)_380px]">
        <section className="min-h-[420px] bg-slate-950">
          <FleetMapPanel
            vehicles={ordered}
            track={track}
            selectedId={selectedId}
            onSelect={setSelectedId}
          />
        </section>

        {/*
          Las dos secciones reparten la altura y cada una hace su propio scroll. Sin min-h-0, una
          flota larga empujaría el panel de alertas fuera de la pantalla, que es justo lo que un
          operador no debe perder de vista.
        */}
        <aside className="flex max-h-[calc(100dvh-56px)] flex-col gap-px bg-slate-800">
          <div className="flex min-h-0 flex-[3] flex-col bg-slate-950">
            <h2 className="shrink-0 px-4 pb-1 pt-3 text-xs font-medium uppercase tracking-wide text-slate-500">
              Flota
            </h2>
            <div className="min-h-0 flex-1 overflow-y-auto">
              <VehicleTable vehicles={ordered} selectedId={selectedId} onSelect={setSelectedId} />
            </div>
          </div>

          <div className="flex min-h-0 flex-[2] flex-col bg-slate-950">
            <h2 className="shrink-0 px-4 pb-1 pt-3 text-xs font-medium uppercase tracking-wide text-slate-500">
              Alertas
            </h2>
            <div className="min-h-0 flex-1 overflow-y-auto">
              <AlertPanel alerts={alerts} />
            </div>
          </div>
        </aside>
      </div>
    </main>
  );
}
