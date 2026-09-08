using FleetTelemetry.Application.Abstractions.Messaging;

namespace FleetTelemetry.Application.Telemetry.ProcessPosition;

/// <summary>
/// Persistir una posición aceptada y evaluar si corresponde alertar.
/// </summary>
public sealed record ProcessPositionCommand(
    string VehicleId,
    double Latitude,
    double Longitude,
    DateTimeOffset RecordedAt,
    string CorrelationId) : ICommand<ProcessPositionResult>;

/// <param name="Skipped">
/// El vehículo está en mitad de una saga de borrado y ya no acepta telemetría.
/// </param>
public sealed record ProcessPositionResult(bool AlertRaised, bool Skipped);
