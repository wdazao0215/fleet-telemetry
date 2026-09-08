"use client";

import { useCallback, useEffect, useRef, useState } from "react";
import { outbox } from "../api/outboxStore";
import { sendBatch } from "../api/ingestion.client";
import { config } from "@/lib/config";
import {
  MAX_ATTEMPTS,
  retryDelayMs,
  selectBatch,
  selectExhausted,
} from "../domain/outboxPolicy";
import { tripStats, type TripStats } from "../domain/tripStats";
import type { LinkState, LocalAlert, QueuedReading } from "../domain/types";

const READING_INTERVAL_MS = 5_000;

interface DriverTelemetry {
  linkState: LinkState;
  pendingCount: number;
  trip: TripStats;
  alerts: LocalAlert[];
  lastPosition: { latitude: number; longitude: number } | null;
  isForcedOffline: boolean;
  toggleForcedOffline: () => void;
  triggerPanic: () => Promise<void>;
}

/**
 * Telemetría del conductor con estrategia offline-first.
 *
 * La lectura **siempre** se escribe primero en IndexedDB y solo después se intenta enviar. Ese orden
 * es lo que hace que un túnel de diez minutos no pierda ni un punto: la red es un detalle de
 * entrega, no la fuente de verdad.
 */
export function useDriverTelemetry(vehicleId: string): DriverTelemetry {
  const [linkState, setLinkState] = useState<LinkState>("online");
  const [pendingCount, setPendingCount] = useState(0);
  const [trip, setTrip] = useState<TripStats>({ distanceMeters: 0, points: 0 });
  const [alerts, setAlerts] = useState<LocalAlert[]>([]);
  const [lastPosition, setLastPosition] = useState<{ latitude: number; longitude: number } | null>(null);
  const [isForcedOffline, setIsForcedOffline] = useState(false);

  const failuresRef = useRef(0);
  const forcedOfflineRef = useRef(false);
  const positionRef = useRef({ latitude: 4.6533, longitude: -74.0836 });
  const sessionReadingsRef = useRef<QueuedReading[]>([]);

  const recordAlert = useCallback(async (alert: LocalAlert) => {
    await outbox.recordAlert(alert);
    setAlerts(await outbox.alerts());
  }, []);

  /** Intenta vaciar la cola. Devuelve si quedó algo pendiente. */
  const flush = useCallback(async (): Promise<void> => {
    if (forcedOfflineRef.current || !navigator.onLine) {
      setLinkState("offline");
      return;
    }

    const queued = await outbox.all();

    // Las lecturas que agotaron sus intentos se abandonan para que no bloqueen la cola.
    const exhausted = selectExhausted(queued);
    if (exhausted.length > 0) {
      await outbox.remove(exhausted.map((reading) => reading.id));
    }

    const batch = selectBatch(queued);
    if (batch.length === 0) {
      setPendingCount(0);
      setLinkState("online");
      return;
    }

    setLinkState("syncing");

    const outcome = await sendBatch(batch);

    if (outcome.settled.length > 0) {
      await outbox.remove(outcome.settled);
    }

    if (outcome.retry.length > 0) {
      await outbox.markAttempted(batch.filter((reading) => outcome.retry.includes(reading.id)));
      failuresRef.current += 1;
      setLinkState("offline");
    } else {
      if (failuresRef.current > 0) {
        void recordAlert({
          id: crypto.randomUUID(),
          kind: "connectionRestored",
          message: `Conexión restablecida. ${outcome.settled.length} lecturas sincronizadas.`,
          at: new Date().toISOString(),
        });
      }

      failuresRef.current = 0;
      setLinkState("online");
    }

    setPendingCount((await outbox.all()).length);
  }, [recordAlert]);

  // Captura de posiciones. Se encolan siempre, haya red o no.
  useEffect(() => {
    let cancelled = false;

    const capture = async () => {
      if (cancelled) {
        return;
      }

      // Deriva simulada: el navegador de un portátil no se mueve, y sin movimiento no se puede
      // demostrar ni el recorrido ni la detección de detención. En un dispositivo real esto sería
      // navigator.geolocation.watchPosition.
      positionRef.current = {
        latitude: positionRef.current.latitude + (Math.random() - 0.45) * 0.0009,
        longitude: positionRef.current.longitude + (Math.random() - 0.5) * 0.0009,
      };

      const reading: QueuedReading = {
        id: crypto.randomUUID(),
        vehicleId,
        latitude: positionRef.current.latitude,
        longitude: positionRef.current.longitude,
        timestamp: new Date().toISOString(),
        attempts: 0,
      };

      await outbox.enqueue(reading);

      sessionReadingsRef.current = [...sessionReadingsRef.current, reading];

      setLastPosition({ latitude: reading.latitude, longitude: reading.longitude });
      setTrip(tripStats(sessionReadingsRef.current));
      setPendingCount((await outbox.all()).length);

      await flush();
    };

    const interval = setInterval(() => void capture(), READING_INTERVAL_MS);
    void capture();

    return () => {
      cancelled = true;
      clearInterval(interval);
    };
  }, [vehicleId, flush]);

  // Reintento con backoff mientras haya cola pendiente.
  useEffect(() => {
    if (pendingCount === 0 || failuresRef.current === 0) {
      return;
    }

    const timer = setTimeout(() => void flush(), retryDelayMs(failuresRef.current));
    return () => clearTimeout(timer);
  }, [pendingCount, flush]);

  useEffect(() => {
    const handleOnline = () => void flush();
    const handleOffline = () => {
      setLinkState("offline");
      void recordAlert({
        id: crypto.randomUUID(),
        kind: "connectionLost",
        message: "Sin conexión. Las lecturas se guardan en el dispositivo.",
        at: new Date().toISOString(),
      });
    };

    window.addEventListener("online", handleOnline);
    window.addEventListener("offline", handleOffline);

    return () => {
      window.removeEventListener("online", handleOnline);
      window.removeEventListener("offline", handleOffline);
    };
  }, [flush, recordAlert]);

  useEffect(() => {
    void outbox.alerts().then(setAlerts);
  }, []);

  const toggleForcedOffline = useCallback(() => {
    setIsForcedOffline((current) => {
      forcedOfflineRef.current = !current;
      return !current;
    });
  }, []);

  const triggerPanic = useCallback(async () => {
    const position = positionRef.current;

    await recordAlert({
      id: crypto.randomUUID(),
      kind: "panic",
      message: "Botón de pánico activado.",
      at: new Date().toISOString(),
    });

    // El pánico no se encola: se intenta enviar de inmediato. Si falla, la alerta local ya quedó
    // registrada y visible para el conductor, que es lo mínimo que debe garantizarse.
    try {
      await fetch(`${config.ingestionApiUrl}/api/v1/panic`, {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
          "X-Api-Key": config.ingestionApiKey,
        },
        body: JSON.stringify({
          vehicleId,
          latitude: position.latitude,
          longitude: position.longitude,
          pressedAt: new Date().toISOString(),
        }),
      });
    } catch {
      // Registrada en local; se reportará cuando vuelva la cobertura.
    }
  }, [recordAlert, vehicleId]);

  return {
    linkState,
    pendingCount,
    trip,
    alerts,
    lastPosition,
    isForcedOffline,
    toggleForcedOffline,
    triggerPanic,
  };
}

export { MAX_ATTEMPTS };
