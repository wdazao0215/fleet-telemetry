import { z } from "zod";
import { config } from "@/lib/config";
import { toApiError } from "@/lib/apiError";
import { VEHICLE_STATUSES, type TrackPoint, type VehicleSnapshot } from "../domain/types";

/**
 * Lo que entra por la red es `unknown` hasta validarse.
 *
 * El backend y el frontend se despliegan por separado, así que sus versiones pueden no coincidir.
 * Sin validación, un campo renombrado se manifestaría como un `undefined` a mitad del render en
 * lugar de como un error claro en el borde.
 */
const vehicleSnapshotSchema = z.object({
  vehicleId: z.string(),
  label: z.string(),
  status: z.enum(VEHICLE_STATUSES),
  lifecycleState: z.enum(["Active", "PendingDeletion", "DeletionFailed", "Deleted"]),
  latitude: z.number().min(-90).max(90).nullable(),
  longitude: z.number().min(-180).max(180).nullable(),
  lastSeenAt: z.string().nullable(),
  stationarySince: z.string().nullable(),
  hasUnacknowledgedAlert: z.boolean(),
});

const trackSchema = z.object({
  vehicleId: z.string(),
  points: z.array(
    z.object({
      latitude: z.number(),
      longitude: z.number(),
      recordedAt: z.string(),
    }),
  ),
});

function authorized(token: string): HeadersInit {
  return { Authorization: `Bearer ${token}` };
}

export async function fetchFleet(token: string, signal?: AbortSignal): Promise<VehicleSnapshot[]> {
  const response = await fetch(`${config.queryApiUrl}/api/v1/vehicles`, {
    headers: authorized(token),
    signal,
  });

  if (!response.ok) {
    throw await toApiError(response);
  }

  return z.array(vehicleSnapshotSchema).parse(await response.json());
}

export async function fetchTrack(
  token: string,
  vehicleId: string,
  signal?: AbortSignal,
): Promise<TrackPoint[]> {
  const response = await fetch(
    `${config.queryApiUrl}/api/v1/vehicles/${encodeURIComponent(vehicleId)}/track?maxPoints=300`,
    { headers: authorized(token), signal },
  );

  if (!response.ok) {
    throw await toApiError(response);
  }

  return trackSchema.parse(await response.json()).points;
}
