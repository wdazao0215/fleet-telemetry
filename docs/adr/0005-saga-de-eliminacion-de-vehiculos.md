# 0005 — Eliminación de vehículos como saga con estado observable

- **Estado:** Aceptada
- **Fecha:** 2026-09-08

## Contexto

El enunciado lo pide explícitamente: *"Explicar (o implementar) cómo manejarías la eliminación de un
vehículo del sistema garantizando que se borre tanto del caché como de la base de datos de
persistencia (Consistencia eventual, patrón Saga, etc.)"*.

Los datos de un vehículo viven en cuatro sitios: la fila en `vehicles`, su histórico en la hypertable
`positions`, sus alertas en `alerts` y su estado caliente en Redis. **No existe transacción que
abarque PostgreSQL y Redis**, así que la atomicidad no es una opción: hay que decidir qué se hace
cuando el borrado falla a mitad de camino.

Además hay una condición de carrera real: entre que se pide el borrado y se ejecuta, el vehículo
puede seguir emitiendo posiciones que resucitarían justo lo que se acaba de purgar.

## Decisión

Una **saga coreografiada en dos pasos, con estado observable y compensación**:

1. **`DeleteVehicleHandler`** (Query.Api): marca el vehículo como `PendingDeletion` en una
   transacción y **después** publica `VehicleDeletionRequested`. Desde ese instante
   `AcceptsTelemetry` es `false`, así que el worker descarta cualquier posición en vuelo.
2. **`CompleteVehicleDeletionHandler`** (worker): purga **caché primero**, después alertas e
   histórico, y por último elimina la fila y publica `VehicleDeletionCompleted`.

Si el paso 2 falla, la compensación **no devuelve el vehículo a `Active`**: lo deja en
`DeletionFailed` con el motivo. Un borrado a medias es un hecho que hay que ver, no que ocultar.

Los tres órdenes son deliberados y están cubiertos por tests:

- **Guardar antes de publicar.** Al revés, se podría procesar el borrado de un vehículo cuya
  transacción luego se revierte.
- **Bloquear la telemetría antes de purgar.** Es lo que cierra la carrera con las posiciones en vuelo.
- **Caché antes que histórico.** La caché es lo que lee el dashboard: el vehículo desaparece de la
  pantalla del operador de inmediato, aunque borrar millones de filas tarde. Al revés, el histórico
  estaría vacío mientras el mapa sigue mostrando un vehículo que ya no existe.

## Alternativas consideradas

| Opción | A favor | En contra | Por qué no |
|---|---|---|---|
| Borrado síncrono en el endpoint | Simple, respuesta inmediata y definitiva | El operador espera a que se borren millones de filas; si Redis falla a mitad, no hay dónde reintentar y el vehículo queda inconsistente en silencio | Convierte un fallo parcial en corrupción silenciosa |
| Two-phase commit | Atomicidad real | Redis no participa en 2PC, y un coordinador bloqueante ata la disponibilidad de los dos almacenes | Técnicamente imposible con esta infraestructura |
| Solo *soft delete* (marcar y no purgar) | Trivial y reversible | Los datos de localización son datos personales: "borrar" sin borrar no cumple una petición de supresión | Incumple la expectativa razonable de un borrado |
| Saga orquestada con una máquina de estados dedicada | Trazabilidad centralizada del proceso | Un componente más que operar para una saga de dos pasos | Desproporcionado; se reconsideraría al llegar al tercer o cuarto paso |

## Consecuencias

**Positivas**
- El borrado sobrevive a que Redis o la base de datos estén caídos: el mensaje espera en la cola.
- Todos los pasos son idempotentes, así que reprocesar el mensaje —la cola entrega *at-least-once*—
  no rompe nada. Verificado: un segundo `DELETE` responde `404`.
- Un fallo deja rastro accionable (`DeletionFailed` + motivo) y se reintenta con otro `DELETE`.

**Negativas**
- **La consistencia es eventual.** Entre el `202` y la purga hay una ventana en la que el vehículo
  existe en unos sitios y no en otros. Por eso la respuesta es `202` y no `204`.
- Si el worker está caído, el vehículo se queda en `PendingDeletion` indefinidamente. Es visible en
  el dashboard, pero nadie lo resuelve solo: falta un proceso de barrido que reintente los que lleven
  demasiado tiempo en ese estado. Está anotado en "Desafíos y Soluciones" del README.
- Depurar el borrado exige seguir un `correlationId` por dos servicios y un broker.

**Reversibilidad**
Alta. El handler del paso 2 se puede invocar de forma síncrona desde el endpoint si algún día se
decide simplificar; la lógica de purga no cambiaría.
