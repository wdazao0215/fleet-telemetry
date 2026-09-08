namespace FleetTelemetry.Contracts.Events;

/// <summary>
/// La ingesta aceptó una posición no duplicada y queda pendiente de procesar.
/// </summary>
/// <remarks>
/// Lleva valores primitivos y no tipos del dominio: el consumidor debe poder deserializarlo aunque
/// su versión de <c>Domain</c> haya cambiado. Es un contrato de transporte, no un modelo.
/// </remarks>
public sealed record PositionAcceptedEvent(
    Guid EventId,
    DateTimeOffset OccurredAt,
    string CorrelationId,
    string VehicleId,
    double Latitude,
    double Longitude,
    DateTimeOffset RecordedAt) : IIntegrationEvent;
