# 0002 — CQRS con dispatcher propio en lugar de MediatR

- **Estado:** Aceptada
- **Fecha:** 2026-09-08

## Contexto

El sistema separa con nitidez escrituras (ingerir una posición, borrar un vehículo) de lecturas
(listar la flota, trazar un recorrido), y esas dos rutas viven además en servicios distintos. CQRS
encaja de forma natural.

La implementación de referencia en .NET es MediatR. Dos hechos condicionan la decisión:

1. MediatR cambió a licencia comercial para uso en producción. Introducirlo en una prueba técnica
   supone añadir una obligación de licencia a quien luego mantenga el código.
2. Del paquete solo se necesitan dos cosas: resolver el handler de un mensaje y encadenar
   comportamientos transversales.

## Decisión

Implementamos el dispatcher en el proyecto: `ICommand<T>`, `IQuery<T>`, sus handlers,
`ICommandDispatcher` y `ICommandPipelineBehavior<TCommand, TResponse>`. Son unas 120 líneas en total.

Las **interfaces** viven en `Application`; la **resolución vía contenedor** vive en
`Infrastructure`, porque necesita `IServiceProvider` y `Application` no puede depender de él.

## Alternativas consideradas

| Opción | A favor | En contra | Por qué no |
|---|---|---|---|
| MediatR | Estándar de facto, conocido por cualquier equipo .NET, pipeline maduro | Licencia comercial; una dependencia externa para resolver una indirección trivial | La obligación de licencia no se justifica por 120 líneas de código |
| Llamar a los handlers directamente por DI | Cero indirección, el stack trace es literal | Cada handler tendría que acordarse de invocar logging, métricas y validación; el día que se olvide uno, nadie lo nota | Los aspectos transversales dejarían de estar garantizados, que es justo lo que aporta el patrón |
| Una alternativa OSS (Wolverine, Brighter) | Mantenidas, con más prestaciones | Traen su propio modelo de mensajería y su curva de aprendizaje | Resuelven un problema mayor que el nuestro; ya tenemos RabbitMQ para la mensajería real |

## Consecuencias

**Positivas**
- Sin obligaciones de licencia y sin dependencia externa en el núcleo.
- El pipeline es explícito y legible: se ve exactamente qué envuelve a cada comando.
- Los tests de aplicación instancian el handler directamente, sin contenedor.

**Negativas**
- Es código propio que hay que mantener y testear; no lo mantiene una comunidad.
- No trae notificaciones, streaming ni el ecosistema de extensiones de MediatR. Si algún día hacen
  falta, habrá que escribirlos.
- Un desarrollador que llega esperando MediatR necesita leer cuatro archivos antes de orientarse.

**Reversibilidad**
Alta. Los handlers implementan interfaces propias con la misma forma que las de MediatR: migrar
sería sustituir la interfaz y el registro de DI, sin tocar la lógica de ningún caso de uso.
