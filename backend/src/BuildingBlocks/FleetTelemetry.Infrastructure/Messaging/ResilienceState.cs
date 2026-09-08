using Polly.CircuitBreaker;

namespace FleetTelemetry.Infrastructure.Messaging;

/// <summary>
/// Estado de la resiliencia del publicador, expuesto para diagnóstico.
/// </summary>
/// <remarks>
/// Existe para que el circuit breaker sea demostrable y no un acto de fe: el endpoint
/// <c>/health/resilience</c> permite apagar Postgres o RabbitMQ y ver en directo cómo se abre el
/// circuito y crece el buffer.
/// </remarks>
public sealed class ResilienceState
{
    internal CircuitBreakerStateProvider PublisherCircuit { get; } = new();

    internal IFallbackBuffer? Buffer { get; set; }

    public string CircuitState => PublisherCircuit.CircuitState.ToString();

    public bool IsHealthy => PublisherCircuit.CircuitState is Polly.CircuitBreaker.CircuitState.Closed;

    public int BufferedMessages => Buffer?.Count ?? 0;

    public long DroppedMessages => Buffer?.DroppedCount ?? 0;
}
