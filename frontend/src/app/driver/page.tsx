"use client";

import { useEffect, useState } from "react";
import { useDriverTelemetry } from "@/features/driver/hooks/useDriverTelemetry";
import { formatDistance } from "@/features/driver/domain/tripStats";
import type { LinkState } from "@/features/driver/domain/types";

const LINK_PRESENTATION: Record<LinkState, { label: string; dot: string; tone: string }> = {
  online: { label: "Conectado", dot: "bg-emerald-400", tone: "text-emerald-300" },
  syncing: { label: "Sincronizando", dot: "bg-sky-400", tone: "text-sky-300" },
  offline: { label: "Sin conexión", dot: "bg-amber-400", tone: "text-amber-300" },
};

const VEHICLE_ID = "VH-DRIVER";

export default function DriverPage() {
  const {
    linkState,
    pendingCount,
    trip,
    alerts,
    lastPosition,
    isForcedOffline,
    toggleForcedOffline,
    triggerPanic,
  } = useDriverTelemetry(VEHICLE_ID);

  const [panicConfirmed, setPanicConfirmed] = useState(false);

  useEffect(() => {
    if (!panicConfirmed) {
      return;
    }

    const timer = setTimeout(() => setPanicConfirmed(false), 4_000);
    return () => clearTimeout(timer);
  }, [panicConfirmed]);

  const link = LINK_PRESENTATION[linkState];

  return (
    <main className="mx-auto flex min-h-dvh w-full max-w-md flex-col gap-4 p-4">
      <header className="flex items-center justify-between">
        <div>
          <h1 className="text-base font-semibold">Panel del conductor</h1>
          <p className="font-mono text-xs text-slate-500">{VEHICLE_ID}</p>
        </div>

        <span className={`inline-flex items-center gap-1.5 text-xs font-medium ${link.tone}`}>
          <span className={`size-2 rounded-full ${link.dot}`} aria-hidden />
          {link.label}
        </span>
      </header>

      <section className="grid grid-cols-2 gap-3">
        <div className="rounded-xl border border-slate-800 bg-slate-900/50 p-4">
          <p className="text-xs uppercase tracking-wide text-slate-500">Viaje actual</p>
          <p className="mt-1 text-2xl font-semibold tabular-nums">
            {formatDistance(trip.distanceMeters)}
          </p>
          <p className="mt-0.5 text-xs text-slate-500">{trip.points} lecturas</p>
        </div>

        <div className="rounded-xl border border-slate-800 bg-slate-900/50 p-4">
          <p className="text-xs uppercase tracking-wide text-slate-500">Sin enviar</p>
          <p className="mt-1 text-2xl font-semibold tabular-nums">{pendingCount}</p>
          <p className="mt-0.5 text-xs text-slate-500">
            {pendingCount > 0 ? "guardadas en el dispositivo" : "todo sincronizado"}
          </p>
        </div>
      </section>

      {lastPosition && (
        <p className="rounded-lg bg-slate-900/50 px-3 py-2 text-center font-mono text-xs text-slate-400">
          {lastPosition.latitude.toFixed(5)}, {lastPosition.longitude.toFixed(5)}
        </p>
      )}

      <button
        onClick={() => {
          void triggerPanic();
          setPanicConfirmed(true);
        }}
        className="rounded-2xl bg-red-600 py-6 text-lg font-bold uppercase tracking-wide text-white shadow-lg transition-transform active:scale-[0.98]"
      >
        {panicConfirmed ? "Alerta enviada" : "Botón de pánico"}
      </button>

      {/*
        Simular la pérdida de cobertura desde la propia app: sin este interruptor, demostrar el
        comportamiento offline exigiría poner el móvil en modo avión durante la sustentación.
      */}
      <label className="flex items-center justify-between rounded-xl border border-slate-800 bg-slate-900/50 px-4 py-3 text-sm">
        <span className="text-slate-300">Simular pérdida de cobertura</span>
        <input
          type="checkbox"
          checked={isForcedOffline}
          onChange={toggleForcedOffline}
          className="size-5 accent-amber-500"
        />
      </label>

      <section className="flex-1">
        <h2 className="pb-2 text-xs font-medium uppercase tracking-wide text-slate-500">
          Historial local
        </h2>

        {alerts.length === 0 ? (
          <p className="py-6 text-center text-sm text-slate-500">Sin eventos registrados.</p>
        ) : (
          <ul className="space-y-2">
            {alerts.map((alert) => (
              <li
                key={alert.id}
                className="rounded-lg border border-slate-800 bg-slate-900/40 px-3 py-2 text-sm"
              >
                <div className="flex items-baseline justify-between gap-2">
                  <span
                    className={
                      alert.kind === "panic"
                        ? "font-medium text-red-300"
                        : alert.kind === "connectionLost"
                          ? "font-medium text-amber-300"
                          : "font-medium text-emerald-300"
                    }
                  >
                    {alert.message}
                  </span>
                  <time className="shrink-0 text-xs tabular-nums text-slate-500">
                    {new Date(alert.at).toLocaleTimeString("es-CO")}
                  </time>
                </div>
              </li>
            ))}
          </ul>
        )}
      </section>
    </main>
  );
}
