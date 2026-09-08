"use client";

import { useCallback, useEffect, useRef, useState } from "react";
import {
  HubConnection,
  HubConnectionBuilder,
  HubConnectionState,
  LogLevel,
} from "@microsoft/signalr";
import { config } from "@/lib/config";
import { fetchFleet } from "../api/fleet.client";
import { fetchAlerts } from "@/features/alerts/api/alerts.client";
import type { Alert } from "@/features/alerts/domain/types";
import { applyUpdate } from "../domain/vehiclePresentation";
import type { VehicleSnapshot, VehicleStateUpdate } from "../domain/types";

export type Transport = "connecting" | "live" | "polling";

const POLLING_INTERVAL_MS = 3_000;
const RECONNECT_DELAY_MS = 5_000;
const MAX_ALERTS_IN_PANEL = 25;

interface FleetLive {
  vehicles: VehicleSnapshot[];
  alerts: Alert[];
  transport: Transport;
  error: string | null;
}

/**
 * Mantiene la flota al día por WebSocket y cae a polling si el canal se pierde.
 *
 * El enunciado admite polling, SSE o WebSockets. Se usa SignalR como transporte principal porque el
 * empuje del servidor evita la latencia de un intervalo fijo, pero un dashboard operativo no puede
 * quedarse en blanco porque un proxy corte los WebSockets: la degradación a polling es automática y
 * el operador no tiene que hacer nada. El indicador de la cabecera dice cuál está activo.
 */
export function useFleetLive(token: string | null): FleetLive {
  const [vehicles, setVehicles] = useState<VehicleSnapshot[]>([]);
  const [alerts, setAlerts] = useState<Alert[]>([]);
  const [transport, setTransport] = useState<Transport>("connecting");
  const [error, setError] = useState<string | null>(null);

  const connectionRef = useRef<HubConnection | null>(null);
  const pollingRef = useRef<ReturnType<typeof setInterval> | null>(null);
  const reconnectRef = useRef<ReturnType<typeof setTimeout> | null>(null);

  const refresh = useCallback(
    async (signal?: AbortSignal) => {
      if (!token) {
        return;
      }

      try {
        const [fleet, recentAlerts] = await Promise.all([
          fetchFleet(token, signal),
          fetchAlerts(token, signal),
        ]);

        setVehicles(fleet);
        setAlerts(recentAlerts);
        setError(null);
      } catch (cause) {
        if (signal?.aborted) {
          return;
        }

        // fetch lanza TypeError cuando no hay red o el servidor no responde, y su mensaje
        // ("Failed to fetch") no le dice nada a un operador. Los errores del backend sí traen un
        // detalle escrito para humanos y se muestran tal cual.
        setError(
          cause instanceof TypeError
            ? "Sin conexión con el servidor. Reintentando…"
            : cause instanceof Error
              ? cause.message
              : "No se pudo consultar la flota.",
        );
      }
    },
    [token],
  );

  const stopPolling = useCallback(() => {
    if (pollingRef.current) {
      clearInterval(pollingRef.current);
      pollingRef.current = null;
    }
  }, []);

  const startPolling = useCallback(() => {
    if (pollingRef.current) {
      return;
    }

    setTransport("polling");
    pollingRef.current = setInterval(() => void refresh(), POLLING_INTERVAL_MS);
  }, [refresh]);

  useEffect(() => {
    if (!token) {
      return;
    }

    const controller = new AbortController();

    const connection = new HubConnectionBuilder()
      .withUrl(`${config.queryApiUrl}/hubs/telemetry`, {
        // El WebSocket del navegador no admite cabeceras propias; SignalR envía el token por query
        // string y el backend solo lo acepta en esta ruta.
        accessTokenFactory: () => token,
      })
      // La política por defecto reintenta a 0, 2, 10 y 30 s y después se rinde para siempre. En un
      // dashboard operativo eso significa quedarse en polling el resto del turno aunque el backend
      // ya haya vuelto. Se reintenta indefinidamente, cada 5 s.
      .withAutomaticReconnect({ nextRetryDelayInMilliseconds: () => RECONNECT_DELAY_MS })
      .configureLogging(LogLevel.Warning)
      .build();

    connection.on("vehicleUpdated", (update: VehicleStateUpdate) => {
      setVehicles((current) => applyUpdate(current, update));
    });

    connection.on("alertRaised", (alert: Alert) => {
      setAlerts((current) => {
        // El backend puede reenviar una alerta si un consumidor reprocesa el mensaje; sin este
        // filtro, el panel mostraría la misma alerta dos veces.
        if (current.some((existing) => existing.id === alert.id)) {
          return current;
        }

        return [alert, ...current].slice(0, MAX_ALERTS_IN_PANEL);
      });

      setVehicles((current) =>
        current.map((vehicle) =>
          vehicle.vehicleId === alert.vehicleId
            ? { ...vehicle, hasUnacknowledgedAlert: true, status: "Alerted" as const }
            : vehicle,
        ),
      );
    });

    connection.onreconnecting(() => startPolling());
    connection.onclose(() => startPolling());

    connection.onreconnected(() => {
      stopPolling();
      setTransport("live");
      // Al recuperar el canal hay un hueco de actualizaciones perdidas: se recarga el estado
      // completo en lugar de seguir con datos que quedaron congelados durante la desconexión.
      void refresh();
    });

    connectionRef.current = connection;

    let disposed = false;

    // withAutomaticReconnect solo cubre las caídas posteriores a una conexión establecida. Si el
    // backend está caído cuando se abre el dashboard, el primer start() falla y no habría nada que
    // reconectar: este bucle cubre ese caso hasta lograr la primera conexión.
    const connectWithRetry = () => {
      if (disposed) {
        return;
      }

      connection
        .start()
        .then(() => {
          if (disposed) {
            return;
          }

          stopPolling();
          setTransport("live");
          // La carga inicial se hace aquí y no en el cuerpo del efecto: llamar a setState de forma
          // síncrona al montar provoca renders en cascada, y React 19 lo señala como error.
          void refresh(controller.signal);
        })
        .catch(() => {
          // No es un error digno de mostrar: es el escenario esperado tras un proxy que no admite
          // WebSockets o un backend que todavía está arrancando. Se sirve por polling mientras.
          void refresh(controller.signal);
          startPolling();
          reconnectRef.current = setTimeout(connectWithRetry, RECONNECT_DELAY_MS);
        });
    };

    connectWithRetry();

    return () => {
      disposed = true;
      controller.abort();
      stopPolling();

      if (reconnectRef.current) {
        clearTimeout(reconnectRef.current);
        reconnectRef.current = null;
      }

      if (connection.state !== HubConnectionState.Disconnected) {
        void connection.stop();
      }

      connectionRef.current = null;
    };
  }, [token, refresh, startPolling, stopPolling]);

  return { vehicles, alerts, transport, error };
}
