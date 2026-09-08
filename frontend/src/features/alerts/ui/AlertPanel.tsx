"use client";

import { ALERT_LABELS, type Alert } from "../domain/types";

export function AlertPanel({ alerts }: { alerts: readonly Alert[] }) {
  if (alerts.length === 0) {
    return (
      <p className="px-4 py-8 text-center text-sm text-slate-500">
        Sin alertas registradas.
      </p>
    );
  }

  return (
    <ul className="divide-y divide-slate-900">
      {alerts.map((alert) => (
        <li key={alert.id} className="px-4 py-3">
          <div className="flex items-baseline justify-between gap-2">
            <span className="font-mono text-sm text-slate-200">{alert.vehicleId}</span>
            <time className="shrink-0 text-xs tabular-nums text-slate-500">
              {new Date(alert.raisedAt).toLocaleTimeString("es-CO")}
            </time>
          </div>
          <p className="mt-0.5 text-xs font-medium text-red-300">{ALERT_LABELS[alert.kind]}</p>
          <p className="mt-0.5 text-xs text-slate-400">{alert.detail}</p>
        </li>
      ))}
    </ul>
  );
}
