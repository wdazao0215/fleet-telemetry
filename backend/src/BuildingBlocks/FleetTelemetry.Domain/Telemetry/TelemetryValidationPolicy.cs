using FleetTelemetry.Domain.Common;
using FleetTelemetry.Domain.Telemetry.Specifications;

namespace FleetTelemetry.Domain.Telemetry;

/// <summary>
/// Reglas temporales que debe cumplir una lectura para ser aceptada.
/// </summary>
/// <remarks>
/// Son configurables porque dependen del despliegue, no del dominio: una flota urbana con buena
/// cobertura tolera mucho menos retraso que uno de larga distancia que sincroniza al salir de zonas
/// sin señal.
/// </remarks>
public sealed class TelemetryValidationPolicy
{
    public static readonly TelemetryValidationPolicy Default = new(TimeSpan.FromMinutes(2), TimeSpan.FromHours(24));

    public TelemetryValidationPolicy(TimeSpan clockSkewTolerance, TimeSpan maximumAge)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(clockSkewTolerance, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(maximumAge, TimeSpan.Zero);

        ClockSkewTolerance = clockSkewTolerance;
        MaximumAge = maximumAge;
    }

    /// <summary>
    /// Margen para relojes adelantados en los dispositivos.
    /// </summary>
    /// <remarks>
    /// Sin tolerancia, un móvil con el reloj unos segundos adelantado vería rechazada cada lectura, y
    /// el fallo aparentaría ser de red. Dos minutos cubren el desajuste típico sin abrir la puerta a
    /// timestamps inventados.
    /// </remarks>
    public TimeSpan ClockSkewTolerance { get; }

    /// <summary>Antigüedad máxima admitida, pensada para los lotes de sincronización offline.</summary>
    public TimeSpan MaximumAge { get; }

    public IReadOnlyList<ISpecification<TelemetryReading>> SpecificationsAt(DateTimeOffset now) =>
    [
        new TimestampNotInFutureSpecification(now, ClockSkewTolerance),
        new TimestampNotTooOldSpecification(now, MaximumAge),
    ];
}
