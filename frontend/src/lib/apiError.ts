import { z } from "zod";

/**
 * ProblemDetails (RFC 7807) tal y como lo devuelve el backend.
 *
 * Se valida en lugar de confiar: si el error viene de un proxy o del propio Next en vez del backend,
 * el cuerpo no tendrá esta forma y mostrar `undefined` al operador sería peor que un mensaje genérico.
 */
const problemDetailsSchema = z.object({
  title: z.string().optional(),
  detail: z.string().optional(),
  status: z.number().optional(),
  code: z.string().optional(),
});

export class ApiError extends Error {
  constructor(
    readonly status: number,
    message: string,
    readonly code?: string,
  ) {
    super(message);
    this.name = "ApiError";
  }
}

export async function toApiError(response: Response): Promise<ApiError> {
  try {
    const parsed = problemDetailsSchema.safeParse(await response.json());

    if (parsed.success) {
      return new ApiError(
        response.status,
        parsed.data.detail ?? parsed.data.title ?? response.statusText,
        parsed.data.code,
      );
    }
  } catch {
    // Cuerpo vacío o no-JSON: se cae al mensaje genérico de abajo.
  }

  return new ApiError(response.status, response.statusText || "Error de red");
}
