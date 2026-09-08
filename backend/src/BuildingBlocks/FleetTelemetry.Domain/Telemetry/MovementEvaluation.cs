namespace FleetTelemetry.Domain.Telemetry;

/// <summary>
/// Resultado de comparar una lectura nueva contra el último movimiento conocido.
/// </summary>
/// <param name="HasMoved">La lectura salió del radio: el vehículo se movió de verdad.</param>
/// <param name="IsStopped">Lleva parado más tiempo del umbral y corresponde alertar.</param>
/// <param name="StationaryFor">Cuánto lleva sin moverse, para poder mostrarlo en el dashboard.</param>
/// <param name="LastMovement">Snapshot que debe persistirse tras evaluar esta lectura.</param>
public readonly record struct MovementEvaluation(
    bool HasMoved,
    bool IsStopped,
    TimeSpan StationaryFor,
    MovementSnapshot LastMovement);
