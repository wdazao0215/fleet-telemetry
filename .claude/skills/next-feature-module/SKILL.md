---
name: next-feature-module
description: >
  Creates a frontend feature module in the Next.js app with its domain/api/hooks/ui layers, Zod
  validation at the network boundary and a Vitest test. Use when the user asks for a new screen,
  panel, widget or frontend feature — "agrega la vista de X", "un panel de alertas", "nueva feature
  en el dashboard".
---

# Feature module en Next.js

Creas una feature del frontend con capas reales, siguiendo `.claude/rules/frontend.md`.

## Estructura

```
src/features/<feature>/
  domain/    types.ts, <rules>.ts        # puro: sin React, sin red
  api/       <feature>.client.ts         # único lugar que conoce las URLs
  hooks/     use<Feature>.ts             # estado y efectos
  ui/        <Component>.tsx             # presentación, recibe props
  __tests__/ <rules>.test.ts
```

## Paso 1 — Domain primero

Define los tipos y las reglas puras antes de tocar React. Aquí van las funciones que deciden cosas
(`resolveVehicleStatus`, `isStale`), y son las que llevan test.

```ts
export type VehicleStatus = "moving" | "stopped" | "alert" | "offline";

export function resolveVehicleStatus(v: VehicleSnapshot, now: number): VehicleStatus { /* ... */ }
```

## Paso 2 — Borde de red con Zod

Lo que entra por la red es `unknown` hasta validarse. El esquema vive junto al cliente:

```ts
const vehicleSnapshotSchema = z.object({
  vehicleId: z.string(),
  latitude: z.number().min(-90).max(90),
  longitude: z.number().min(-180).max(180),
  recordedAt: z.string().datetime(),
});

export async function fetchVehicles(): Promise<VehicleSnapshot[]> {
  const res = await fetch(`${API_URL}/api/v1/vehicles`, { headers: authHeaders() });
  if (!res.ok) throw new ApiError(res.status, await res.text());
  return z.array(vehicleSnapshotSchema).parse(await res.json());
}
```

## Paso 3 — Hook

Encapsula estado, suscripción a SignalR y limpieza. Si la feature es de tiempo real, el hook usa
`useTelemetryStream`, que ya resuelve la degradación a polling.

## Paso 4 — UI

Componentes de presentación. Sin `fetch`, sin lógica de decisión: llaman a `domain/` cuando necesitan
derivar algo. Estados con color **y** etiqueta de texto.

## Paso 5 — Test

Vitest sobre `domain/`. Testing Library solo si el componente tiene interacción real.

## Verificación

`npm run build`, `npm test` y `npm run lint`. Después abre la página y compruébalo de verdad: la skill
`telemetry-verify` levanta el entorno completo.

## Errores a evitar

- `fetch` dentro de un `.tsx`.
- `any` o `as` para callar al compilador.
- Importar Leaflet sin `dynamic(..., { ssr: false })`: toca `window` al importarse y rompe el build.
