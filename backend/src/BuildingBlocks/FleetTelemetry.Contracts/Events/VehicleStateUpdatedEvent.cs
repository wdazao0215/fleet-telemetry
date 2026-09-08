namespace FleetTelemetry.Contracts.Events;

/// <summary>
/// El worker terminó de procesar una posición y publica el estado resultante del vehículo.
/// </summary>
/// <remarks>
/// Es distinto de <see cref="PositionAcceptedEvent"/>: aquel es una lectura cruda recién validada,
/// este lleva ya el estado derivado (en movimiento, detenido, alertado). El dashboard consume este
/// porque es exactamente lo que pinta; si consumiera el crudo tendría que recalcular la regla de
/// detención en el navegador, duplicando lógica de negocio fuera del backend.
/// </remarks>
public sealed record VehicleStateUpdatedEvent(
    Guid EventId,
    DateTimeOffset OccurredAt,
    string CorrelationId,
    string VehicleId,
    double Latitude,
    double Longitude,
    DateTimeOffset RecordedAt,
    string Status,
    DateTimeOffset? StationarySince) : IIntegrationEvent;
