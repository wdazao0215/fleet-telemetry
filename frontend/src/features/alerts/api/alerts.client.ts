import { z } from "zod";
import { config } from "@/lib/config";
import { toApiError } from "@/lib/apiError";
import type { Alert } from "../domain/types";

const alertSchema = z.object({
  id: z.string(),
  vehicleId: z.string(),
  kind: z.enum(["StoppedVehicle", "PanicButton"]),
  latitude: z.number(),
  longitude: z.number(),
  raisedAt: z.string(),
  acknowledgedAt: z.string().nullable(),
  detail: z.string(),
});

export async function fetchAlerts(token: string, signal?: AbortSignal): Promise<Alert[]> {
  const response = await fetch(`${config.queryApiUrl}/api/v1/alerts?limit=25`, {
    headers: { Authorization: `Bearer ${token}` },
    signal,
  });

  if (!response.ok) {
    throw await toApiError(response);
  }

  return z.array(alertSchema).parse(await response.json());
}
