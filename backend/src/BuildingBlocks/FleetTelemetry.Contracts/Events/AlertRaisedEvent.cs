namespace FleetTelemetry.Contracts.Events;

public sealed record AlertRaisedEvent(
    Guid EventId,
    DateTimeOffset OccurredAt,
    string CorrelationId,
    Guid AlertId,
    string VehicleId,
    string Kind,
    double Latitude,
    double Longitude,
    DateTimeOffset RaisedAt,
    string Detail) : IIntegrationEvent;
