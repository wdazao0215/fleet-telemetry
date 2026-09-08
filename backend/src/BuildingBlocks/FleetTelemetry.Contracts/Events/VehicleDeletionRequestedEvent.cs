namespace FleetTelemetry.Contracts.Events;

/// <summary>
/// Primer paso de la saga de eliminación: la base de datos ya marcó el vehículo como pendiente.
/// </summary>
public sealed record VehicleDeletionRequestedEvent(
    Guid EventId,
    DateTimeOffset OccurredAt,
    string CorrelationId,
    string VehicleId) : IIntegrationEvent;
