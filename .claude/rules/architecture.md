# Regla: arquitectura y capas

## Dirección de las dependencias

```
Domain  ←  Application  ←  Infrastructure  ←  Services
```

Las flechas apuntan hacia adentro. Nada apunta hacia afuera. `FleetTelemetry.Architecture.Tests` lo
verifica con NetArchTest y **rompe el build** si se incumple.

## Qué va en cada capa

### Domain (`FleetTelemetry.Domain`)

Entidades (`Vehicle`), value objects (`Coordinate`, `VehicleId`), eventos de dominio,
Specifications de validación, y excepciones de dominio.

- Cero paquetes NuGet más allá del BCL.
- Sin atributos de EF Core, sin `[JsonPropertyName]`, sin `ILogger`.
- Todo debe ser testeable instanciando objetos, sin contenedor de DI ni base de datos.
- Las invariantes se protegen en el constructor o en un factory `Create` que devuelve `Result<T>`.

### Application (`FleetTelemetry.Application`)

Casos de uso como comandos y queries, sus handlers, los **puertos** y los behaviors del pipeline.

- Define interfaces (`IPositionRepository`, `IPositionCache`, `IEventBus`, `IClock`). Nunca las
  implementa.
- Un handler por caso de uso. Si un handler pasa de ~60 líneas, hay una regla de dominio escapándose
  hacia arriba: bájala a `Domain`.
- Los comandos mutan y devuelven lo mínimo; las queries no mutan nunca.

### Infrastructure (`FleetTelemetry.Infrastructure`)

Adaptadores: EF Core + TimescaleDB, Redis, RabbitMQ, pipelines de Polly, reloj del sistema.

- **`AppDbContext`, `IConnectionMultiplexer` y `IConnection` de RabbitMQ no se exponen fuera de este
  proyecto.** Si un servicio necesita datos, pide un puerto.
- Cada adaptador implementa exactamente un puerto.
- La política de resiliencia se declara aquí, no dentro de la lógica de negocio.

### Services (`Ingestion.Api`, `Processing.Worker`, `Query.Api`, `Migrator`)

Hosts. Composición de DI, endpoints, mapeo HTTP, configuración, health checks.

- Un endpoint valida la forma del request, invoca al dispatcher y traduce el resultado a HTTP. Nada
  más.
- Si ves un `if` de negocio en un endpoint, está en la capa equivocada.

## CQRS

Dispatcher propio, no MediatR (ver `docs/adr/0002`).

```csharp
public interface ICommandHandler<in TCommand, TResult> where TCommand : ICommand<TResult>;
public interface IQueryHandler<in TQuery, TResult>   where TQuery   : IQuery<TResult>;
```

Los *cross-cutting concerns* (validación, logging, métricas) van como behaviors del pipeline, nunca
copiados dentro de cada handler.

## Manejo de errores

`Result<T>` para fallos esperados (validación, duplicado, no encontrado). Excepciones solo para lo
verdaderamente excepcional. Un duplicado de GPS no es una excepción: es un resultado normal del
sistema y ocurre en el 10% del tráfico.
