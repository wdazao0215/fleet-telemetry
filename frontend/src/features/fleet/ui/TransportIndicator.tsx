import type { Transport } from "../hooks/useFleetLive";

const PRESENTATION: Record<Transport, { label: string; dot: string; title: string }> = {
  live: {
    label: "En vivo",
    dot: "bg-emerald-400",
    title: "Conectado por WebSocket: el servidor empuja cada cambio.",
  },
  polling: {
    label: "Polling",
    dot: "bg-amber-400",
    title: "Sin WebSocket disponible: consultando cada 3 segundos.",
  },
  connecting: {
    label: "Conectando",
    dot: "bg-slate-500",
    title: "Estableciendo el canal en tiempo real.",
  },
};

/**
 * Dice qué transporte está activo.
 *
 * La degradación a polling es automática, pero silenciarla sería engañoso: si el operador ve datos
 * con tres segundos de retraso, merece saber por qué.
 */
export function TransportIndicator({ transport }: { transport: Transport }) {
  const presentation = PRESENTATION[transport];

  return (
    <span
      title={presentation.title}
      className="inline-flex items-center gap-1.5 rounded-full bg-slate-800/80 px-2.5 py-1 text-xs text-slate-300"
    >
      <span className={`size-1.5 rounded-full ${presentation.dot}`} aria-hidden />
      {presentation.label}
    </span>
  );
}
