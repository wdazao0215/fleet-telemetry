namespace FleetTelemetry.Contracts.Events;

/// <summary>
/// El conductor pulsó el botón de pánico.
/// </summary>
/// <remarks>
/// Viaja por la cola como cualquier otro evento en lugar de escribirse directamente en la base de
/// datos desde la ingesta. Puede parecer contraintuitivo para algo urgente, pero es justo al revés:
/// si la base de datos está caída, una escritura directa devolvería un error al conductor y la
/// alerta se perdería. Encolada, sobrevive a la caída y se procesa en cuanto el sistema se recupere.
/// </remarks>
public sealed record PanicButtonPressedEvent(
    Guid EventId,
    DateTimeOffset OccurredAt,
    string CorrelationId,
    string VehicleId,
    double Latitude,
    double Longitude,
    DateTimeOffset PressedAt) : IIntegrationEvent;
