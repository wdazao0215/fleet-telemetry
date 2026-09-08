namespace FleetTelemetry.Infrastructure.Messaging;

public sealed class RabbitMqOptions
{
    public const string SectionName = "RabbitMq";

    public string Host { get; set; } = "localhost";

    public int Port { get; set; } = 5672;

    public string Username { get; set; } = "guest";

    public string Password { get; set; } = "guest";

    public string VirtualHost { get; set; } = "/";

    /// <summary>Reintentos antes de considerar que el broker no está disponible.</summary>
    public int MaxRetryAttempts { get; set; } = 3;

    public int RetryBaseDelayMilliseconds { get; set; } = 200;

    /// <summary>Proporción de fallos en la ventana que abre el circuito.</summary>
    public double CircuitFailureRatio { get; set; } = 0.5;

    public int CircuitSamplingDurationSeconds { get; set; } = 30;

    /// <summary>Mínimo de intentos en la ventana antes de que la proporción signifique algo.</summary>
    /// <remarks>Sin este mínimo, un único fallo aislado abriría el circuito al 100% de fallos.</remarks>
    public int CircuitMinimumThroughput { get; set; } = 5;

    public int CircuitBreakDurationSeconds { get; set; } = 15;

    /// <summary>
    /// Mensajes que caben en el buffer local mientras el broker no responde.
    /// </summary>
    /// <remarks>
    /// Acotado a propósito: un buffer ilimitado convierte una caída del broker en una caída por
    /// memoria del servicio de ingesta, que es peor que perder las lecturas más antiguas.
    /// </remarks>
    public int FallbackBufferCapacity { get; set; } = 10_000;

    public int FallbackDrainIntervalSeconds { get; set; } = 5;
}
