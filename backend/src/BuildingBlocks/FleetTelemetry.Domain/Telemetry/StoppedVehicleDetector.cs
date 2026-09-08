namespace FleetTelemetry.Domain.Telemetry;

/// <summary>
/// Decide si un vehículo se ha movido y si lleva detenido lo suficiente como para alertar.
/// </summary>
/// <remarks>
/// Es una función pura sobre el último movimiento conocido y la lectura entrante. Al no depender de
/// Redis ni del reloj del sistema, la regla más importante del enunciado se puede testear en
/// milisegundos y sobre todos sus bordes, que es donde de verdad se rompen estas cosas.
/// </remarks>
public static class StoppedVehicleDetector
{
    public static MovementEvaluation Evaluate(
        MovementSnapshot? lastMovement,
        TelemetryReading reading,
        StoppedVehiclePolicy policy)
    {
        ArgumentNullException.ThrowIfNull(reading);
        ArgumentNullException.ThrowIfNull(policy);

        // Primera lectura del vehículo: no hay historia contra la que comparar, así que se toma como
        // punto de partida y nunca se alerta. Alertar aquí sería inventar un pasado que no vimos.
        if (lastMovement is null)
        {
            return new MovementEvaluation(
                HasMoved: true,
                IsStopped: false,
                StationaryFor: TimeSpan.Zero,
                LastMovement: new MovementSnapshot(reading.Position, reading.RecordedAt));
        }

        var distance = lastMovement.Position.DistanceInMetersTo(reading.Position);

        if (distance > policy.RadiusInMeters)
        {
            return new MovementEvaluation(
                HasMoved: true,
                IsStopped: false,
                StationaryFor: TimeSpan.Zero,
                LastMovement: new MovementSnapshot(reading.Position, reading.RecordedAt));
        }

        // Dentro del radio: sigue parado. El snapshot NO se actualiza, porque es justamente el
        // ancla desde la que se mide cuánto lleva sin moverse.
        var stationaryFor = reading.RecordedAt - lastMovement.ObservedAt;

        // Una lectura que llega desordenada (típico al drenar una cola offline) daría una duración
        // negativa; se trata como cero en vez de como un vehículo detenido desde el futuro.
        if (stationaryFor < TimeSpan.Zero)
        {
            stationaryFor = TimeSpan.Zero;
        }

        return new MovementEvaluation(
            HasMoved: false,
            IsStopped: stationaryFor >= policy.Threshold,
            StationaryFor: stationaryFor,
            LastMovement: lastMovement);
    }
}
