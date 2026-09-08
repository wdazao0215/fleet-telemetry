using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FleetTelemetry.Infrastructure.Messaging;

/// <summary>
/// Vacía el buffer local hacia el broker cuando este vuelve a estar disponible.
/// </summary>
/// <remarks>
/// Sin este servicio, el buffer sería un agujero negro: la ingesta no caería, pero los datos
/// acumulados durante la caída no llegarían nunca a persistirse. Es la mitad que completa el patrón.
/// </remarks>
internal sealed class FallbackDrainService(
    IFallbackBuffer buffer,
    IMessagePublisher eventBus,
    IOptions<RabbitMqOptions> options,
    ILogger<FallbackDrainService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromSeconds(options.Value.FallbackDrainIntervalSeconds);
        using var timer = new PeriodicTimer(interval);

        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
        {
            if (buffer.Count == 0)
            {
                continue;
            }

            var drained = 0;

            await foreach (var message in buffer.DrainAsync(stoppingToken).ConfigureAwait(false))
            {
                try
                {
                    await eventBus
                        .PublishRawAsync(message.RoutingKey, message.Payload, message.EventType, stoppingToken)
                        .ConfigureAwait(false);
                    drained++;
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    // El broker sigue sin responder. Se devuelve el mensaje al buffer y se corta el
                    // drenaje: seguir intentando con los demás solo repetiría el mismo fallo.
                    buffer.TryEnqueue(message);
                    logger.DrainPaused(exception);
                    break;
                }
            }

            if (drained > 0)
            {
                logger.BufferDrained(drained, buffer.Count);
            }
        }
    }
}
