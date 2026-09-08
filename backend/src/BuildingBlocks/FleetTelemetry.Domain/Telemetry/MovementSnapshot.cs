namespace FleetTelemetry.Domain.Telemetry;

/// <summary>
/// Última posición en la que se confirmó movimiento real del vehículo.
/// </summary>
/// <remarks>
/// Es deliberadamente distinta de "última posición recibida": un vehículo detenido sigue emitiendo
/// posiciones cada pocos segundos. Comparar contra la última recibida haría que el cronómetro de
/// detención se reiniciase constantemente y la alerta no se emitiría nunca.
/// </remarks>
public sealed record MovementSnapshot(Coordinate Position, DateTimeOffset ObservedAt);
