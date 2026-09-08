namespace FleetTelemetry.Domain.Telemetry;

/// <summary>
/// Umbrales que definen cuándo se considera que un vehículo está detenido.
/// </summary>
public sealed class StoppedVehiclePolicy
{
    /// <summary>El enunciado fija el minuto; el radio sale de la precisión típica de un GPS civil.</summary>
    public static readonly StoppedVehiclePolicy Default = new(10, TimeSpan.FromMinutes(1));

    public StoppedVehiclePolicy(double radiusInMeters, TimeSpan threshold)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(radiusInMeters);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(threshold, TimeSpan.Zero);

        RadiusInMeters = radiusInMeters;
        Threshold = threshold;
    }

    /// <summary>
    /// Radio por debajo del cual dos lecturas se consideran el mismo punto.
    /// </summary>
    /// <remarks>
    /// Un GPS civil parado deriva varios metros por efecto multipath, sobre todo entre edificios.
    /// Con radio cero, ese ruido se leería como movimiento y ningún vehículo estaría nunca detenido.
    /// </remarks>
    public double RadiusInMeters { get; }

    public TimeSpan Threshold { get; }
}
