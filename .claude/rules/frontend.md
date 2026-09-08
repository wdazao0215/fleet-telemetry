# Regla: frontend

## Organización por feature

```
src/features/<feature>/
  domain/   # tipos y reglas puras — sin React, sin fetch
  api/      # cliente HTTP / SignalR, único lugar que conoce las URLs
  hooks/    # estado y efectos, consumen api/
  ui/       # componentes de presentación
```

Un componente de `ui/` recibe datos por props y no sabe de dónde vienen. La regla práctica: si borras
la carpeta `api/`, `domain/` debe seguir compilando y sus tests pasando.

## TypeScript

- `strict: true`. **`any` está prohibido**; si de verdad no se conoce el tipo, `unknown` y se estrecha.
- Los tipos del backend viven en `domain/` y se validan en el borde con Zod. Lo que entra por la red
  es `unknown` hasta que se valida.
- Sin aserciones `as` para silenciar al compilador.

## React

- **Nada de `fetch` dentro de un componente.** Va en `api/` y se consume mediante un hook.
- Server Components por defecto; `"use client"` solo donde hay estado, efectos o eventos.
- Leaflet solo en cliente: `dynamic(() => import(...), { ssr: false })`, porque toca `window` al
  importarse.
- Las claves de lista son identificadores estables, nunca el índice del array.

## Tiempo real

SignalR es el transporte principal. Si la conexión falla o el navegador no lo soporta, el hook
degrada **automáticamente** a polling. El usuario no debería notar cuál de los dos está activo.

## Estilos

Tailwind. Estados de vehículo con color **y** con etiqueta de texto: el color por sí solo no es
accesible para daltonismo, y un dashboard operativo se mira bajo presión.
