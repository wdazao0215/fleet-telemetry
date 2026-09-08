using FleetTelemetry.Application.Abstractions.Ports;
using FleetTelemetry.Contracts;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;

namespace FleetTelemetry.Infrastructure.Messaging;

/// <summary>
/// Publicador tolerante a fallos: reintenta, corta el circuito y, si nada funciona, encola en local.
/// </summary>
/// <remarks>
/// Es la respuesta al requisito del enunciado: "si la base de datos de persistencia falla
/// temporalmente o el servicio de ruteo está inaccesible, el sistema de ingesta no debe caer".
///
/// La secuencia es intencionada. Los reintentos con backoff cubren el fallo transitorio —una
/// reconexión, un pico de latencia—. El circuit breaker corta cuando el fallo deja de ser
/// transitorio: sin él, cada petición esperaría el timeout completo del broker y la ingesta moriría
/// por agotamiento de hilos aunque técnicamente "no hubiese caído". Con el circuito abierto, el
/// fallo es inmediato y barato, y el mensaje va al buffer.
/// </remarks>
internal sealed class ResilientEventBus : IEventBus
{
    private readonly IMessagePublisher inner;
    private readonly IFallbackBuffer buffer;
    private readonly ResiliencePipeline pipeline;
    private readonly ILogger<ResilientEventBus> logger;

    public ResilientEventBus(
        IMessagePublisher inner,
        IFallbackBuffer buffer,
        ResilienceState state,
        IOptions<RabbitMqOptions> options,
        ILogger<ResilientEventBus> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(state);

        this.inner = inner;
        this.buffer = buffer;
        this.logger = logger;

        var settings = options.Value;
        state.Buffer = buffer;

        pipeline = new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = settings.MaxRetryAttempts,
                Delay = TimeSpan.FromMilliseconds(settings.RetryBaseDelayMilliseconds),
                BackoffType = DelayBackoffType.Exponential,
                // Con jitter: sin él, todas las peticiones que fallaron a la vez reintentarían a la
                // vez y golpearían al broker justo cuando intenta recuperarse.
                UseJitter = true,
            })
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions
            {
                FailureRatio = settings.CircuitFailureRatio,
                SamplingDuration = TimeSpan.FromSeconds(settings.CircuitSamplingDurationSeconds),
                MinimumThroughput = settings.CircuitMinimumThroughput,
                BreakDuration = TimeSpan.FromSeconds(settings.CircuitBreakDurationSeconds),
                StateProvider = state.PublisherCircuit,
                OnOpened = arguments =>
                {
                    this.logger.CircuitOpened(arguments.BreakDuration);
                    return ValueTask.CompletedTask;
                },
                OnClosed = _ =>
                {
                    this.logger.CircuitClosed();
                    return ValueTask.CompletedTask;
                },
            })
            .Build();
    }

    public async Task PublishAsync<TEvent>(
        TEvent integrationEvent,
        string routingKey,
        CancellationToken cancellationToken)
        where TEvent : IIntegrationEvent
    {
        var payload = RabbitMqEventBus.Serialize(integrationEvent);
        var eventType = typeof(TEvent).Name;

        try
        {
            await pipeline.ExecuteAsync(
                async token => await inner.PublishRawAsync(routingKey, payload, eventType, token).ConfigureAwait(false),
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // Aquí está el punto del requisito: el fallo del broker NO se propaga al llamador. La
            // ingesta responde 202 y el mensaje espera en el buffer a que el circuito se recupere.
            var queued = buffer.TryEnqueue(
                new PendingMessage(routingKey, payload, eventType, DateTimeOffset.UtcNow));

            if (!queued)
            {
                logger.BufferOverflow(eventType, buffer.DroppedCount);
            }
            else if (exception is BrokenCircuitException)
            {
                logger.BufferedWhileCircuitOpen(eventType, buffer.Count);
            }
            else
            {
                logger.BufferedAfterFailure(exception, eventType, buffer.Count);
            }
        }
    }
}
