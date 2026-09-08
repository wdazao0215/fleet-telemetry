using FleetTelemetry.Domain.Telemetry;
using FleetTelemetry.Domain.Vehicles;

namespace FleetTelemetry.Application.Abstractions.Models;

/// <summary>
/// Último estado conocido de un vehículo, tal como se guarda en caché para el dashboard.
/// </summary>
/// <remarks>
/// El dashboard pregunta por "dónde está ahora la flota" varias veces por segundo. Servir eso desde
/// la hypertable obligaría a un DISTINCT ON sobre millones de filas en cada refresco; en caché es
/// una lectura directa por clave.
/// </remarks>
public sealed record LivePosition(
    VehicleId VehicleId,
    Coordinate Position,
    DateTimeOffset RecordedAt,
    MovementSnapshot? LastMovement);
