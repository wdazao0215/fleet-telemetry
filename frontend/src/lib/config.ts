/**
 * Configuración pública del cliente.
 *
 * Se lee de variables NEXT_PUBLIC_* porque el dashboard corre en el navegador y la URL de la API
 * cambia entre local, staging y producción. Nada sensible puede vivir aquí: todo lo que se prefija
 * con NEXT_PUBLIC_ queda incrustado en el bundle y es visible para cualquiera.
 */
export const config = {
  queryApiUrl: process.env.NEXT_PUBLIC_QUERY_API_URL ?? "http://localhost:8082",
  ingestionApiUrl: process.env.NEXT_PUBLIC_INGESTION_API_URL ?? "http://localhost:8081",
  /** Clave de dispositivo para la PWA del conductor; en producción sería una por vehículo. */
  ingestionApiKey: process.env.NEXT_PUBLIC_INGESTION_API_KEY ?? "dev-fleet-key",
} as const;
