---
name: dotnet-vertical-slice
description: >
  Creates a complete CQRS use case in the FleetTelemetry .NET solution — command or query, handler,
  validation Specification, port usage, endpoint wiring and unit test — respecting the Clean
  Architecture layer rule. Use when the user asks for a new endpoint, a new use case, a new command
  or query, or says "agrega el caso de uso X", "nuevo endpoint para Y", "necesito una query de Z".
---

# Vertical slice CQRS en .NET

Creas un caso de uso completo atravesando las capas, sin romper la dirección de las dependencias
descrita en `.claude/rules/architecture.md`.

## Paso 1 — Determinar la naturaleza

**¿Muta estado o solo lee?** Un comando muta y devuelve lo mínimo. Una query no muta jamás. Si algo
"lee y de paso actualiza un contador", son dos cosas: sepáralas.

Confirma antes de escribir:
1. Nombre del caso de uso en imperativo (`IngestPosition`, `DeleteVehicle`, `GetActiveVehicles`).
2. Servicio host (`Ingestion.Api`, `Query.Api`, `Processing.Worker`).
3. Puertos que necesita (`IPositionRepository`, `IPositionCache`, `IEventBus`, `IClock`).
4. Reglas de negocio implicadas — y si alguna pertenece a `Domain`, bájala.

## Paso 2 — Archivos a generar

```
src/BuildingBlocks/FleetTelemetry.Application/<Feature>/
  <Name>Command.cs          o  <Name>Query.cs
  <Name>Handler.cs
  <Name>Validator.cs        ← Specification, no atributos de validación
src/BuildingBlocks/FleetTelemetry.Domain/<Feature>/
  (solo si aparece una regla o value object nuevo)
src/Services/<Host>/Endpoints/<Feature>Endpoints.cs
tests/FleetTelemetry.Application.Tests/<Feature>/<Name>HandlerTests.cs
```

## Paso 3 — Plantillas

```csharp
public sealed record IngestPositionCommand(string VehicleId, double Latitude, double Longitude,
    DateTimeOffset Timestamp) : ICommand<IngestPositionResult>;

public sealed class IngestPositionHandler(
    IPositionCache cache,
    IEventBus eventBus,
    IClock clock) : ICommandHandler<IngestPositionCommand, IngestPositionResult>
{
    public async Task<Result<IngestPositionResult>> HandleAsync(
        IngestPositionCommand command, CancellationToken cancellationToken)
    {
        // ...
    }
}
```

Endpoint — traduce HTTP y nada más:

```csharp
group.MapPost("/telemetry", async (IngestPositionRequest request, ICommandDispatcher dispatcher,
        CancellationToken ct) =>
    {
        var result = await dispatcher.SendAsync(request.ToCommand(), ct);
        return result.IsSuccess
            ? Results.Accepted(value: result.Value)
            : result.Error.ToProblemDetails();
    })
    .WithName("IngestPosition")
    .Produces<IngestPositionResult>(StatusCodes.Status202Accepted)
    .ProducesProblem(StatusCodes.Status400BadRequest);
```

## Paso 4 — Test obligatorio

Un test del camino feliz y **uno por cada borde de la regla de negocio**. Los puertos se sustituyen
con NSubstitute; nunca se toca base de datos en `Application.Tests`.

## Verificación

`dotnet build` y `dotnet test` deben pasar antes de dar por cerrado el slice. Si el caso de uso es
observable desde fuera, ejecútalo también con la skill `telemetry-verify`.

## Errores a evitar

- Meter `DbContext` o `IConnectionMultiplexer` en `Application`: usa el puerto.
- Lanzar excepciones para fallos esperados: devuelve `Result<T>`.
- Un handler que crece a 100 líneas: hay lógica de dominio que debe bajar a `Domain`.
