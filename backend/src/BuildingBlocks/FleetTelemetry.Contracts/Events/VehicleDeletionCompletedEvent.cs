namespace FleetTelemetry.Contracts.Events;

/// <summary>
/// La caché quedó purgada y el histórico eliminado: la saga puede consumarse.
/// </summary>
public sealed record VehicleDeletionCompletedEvent(
    Guid EventId,
    DateTimeOffset OccurredAt,
    string CorrelationId,
    string VehicleId,
    bool Succeeded,
    string? FailureReason) : IIntegrationEvent;
