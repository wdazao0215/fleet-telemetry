import { config } from "@/lib/config";
import { toApiError } from "@/lib/apiError";
import type { Session } from "../domain/types";
import { z } from "zod";

const sessionSchema = z.object({
  accessToken: z.string().min(1),
  expiresAt: z.string(),
});

export async function requestToken(username: string, password: string): Promise<Session> {
  const response = await fetch(`${config.queryApiUrl}/api/v1/auth/token`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ username, password }),
  });

  if (!response.ok) {
    throw await toApiError(response);
  }

  return sessionSchema.parse(await response.json());
}
