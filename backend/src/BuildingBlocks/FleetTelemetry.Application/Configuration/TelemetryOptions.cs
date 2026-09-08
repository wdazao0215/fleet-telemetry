using FleetTelemetry.Domain.Telemetry;

namespace FleetTelemetry.Application.Configuration;

/// <summary>
/// Parámetros de negocio ajustables sin recompilar.
/// </summary>
/// <remarks>
/// Estos valores se afinan en operación —una flota urbana y una de larga distancia no comparten
/// umbrales— así que viven en configuración. Las políticas de dominio se construyen a partir de
/// ellos para que el dominio siga sin conocer el sistema de configuración.
/// </remarks>
public sealed class TelemetryOptions
{
    public const string SectionName = "Telemetry";

    /// <summary>Ventana durante la que se recuerda un paquete para detectar su reenvío.</summary>
    public int DeduplicationWindowSeconds { get; set; } = 10;

    public double StoppedRadiusMeters { get; set; } = 10;

    public int StoppedThresholdSeconds { get; set; } = 60;

    /// <summary>Silencio entre alertas repetidas del mismo tipo para un mismo vehículo.</summary>
    public int AlertCooldownSeconds { get; set; } = 300;

    /// <summary>Vigencia del último estado conocido en caché.</summary>
    public int LiveStateTtlSeconds { get; set; } = 300;

    /// <summary>
    /// Vigencia del ancla de movimiento.
    /// </summary>
    /// <remarks>
    /// Debe ser holgada: si el ancla caduca mientras el vehículo sigue parado, el cronómetro de
    /// detención se reinicia y la alerta se retrasa un ciclo entero.
    /// </remarks>
    public int MovementAnchorTtlSeconds { get; set; } = 21_600;

    public int ClockSkewToleranceSeconds { get; set; } = 120;

    public int MaximumReadingAgeHours { get; set; } = 24;

    public TimeSpan DeduplicationWindow => TimeSpan.FromSeconds(DeduplicationWindowSeconds);

    public TimeSpan AlertCooldown => TimeSpan.FromSeconds(AlertCooldownSeconds);

    public TimeSpan LiveStateTtl => TimeSpan.FromSeconds(LiveStateTtlSeconds);

    public TimeSpan MovementAnchorTtl => TimeSpan.FromSeconds(MovementAnchorTtlSeconds);

    public StoppedVehiclePolicy StoppedPolicy() =>
        new(StoppedRadiusMeters, TimeSpan.FromSeconds(StoppedThresholdSeconds));

    public TelemetryValidationPolicy ValidationPolicy() =>
        new(TimeSpan.FromSeconds(ClockSkewToleranceSeconds), TimeSpan.FromHours(MaximumReadingAgeHours));
}
