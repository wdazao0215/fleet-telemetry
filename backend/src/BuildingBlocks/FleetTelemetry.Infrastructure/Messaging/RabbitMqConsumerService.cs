using System.Text;
using System.Text.Json;
using FleetTelemetry.Contracts;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace FleetTelemetry.Infrastructure.Messaging;

/// <summary>
/// Consume una cola y entrega cada mensaje deserializado a un manejador.
/// </summary>
/// <remarks>
/// Base común de los consumidores del worker. Resuelve tres cosas que se hacen mal con facilidad:
/// el prefetch, la política de acuse y la reconexión.
/// </remarks>
public abstract partial class RabbitMqConsumerService<TEvent>(
    RabbitMqConnectionProvider connectionProvider,
    ILogger logger) : BackgroundService
    where TEvent : class, IIntegrationEvent
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Mensajes que el broker entrega sin esperar acuse.
    /// </summary>
    /// <remarks>
    /// Sin límite, RabbitMQ empujaría la cola entera a un consumidor y la memoria del worker crecería
    /// con el backlog; además impediría repartir la carga si se escalan varias réplicas.
    /// </remarks>
    protected virtual ushort PrefetchCount => 32;

    protected abstract string QueueName { get; }

    /// <summary>
    /// Cuando es <c>true</c>, la instancia declara su propia cola efímera en lugar de compartir una
    /// duradera.
    /// </summary>
    /// <remarks>
    /// Es la diferencia entre repartir trabajo y difundir. Un worker que procesa posiciones quiere
    /// que cada mensaje lo atienda una sola réplica; un servidor de WebSockets necesita que **todas**
    /// las réplicas reciban todo, o los navegadores conectados a una instancia se perderían las
    /// actualizaciones que consumió otra.
    /// </remarks>
    protected virtual bool UsesExclusiveQueue => false;

    /// <summary>Routing key a la que se enlaza la cola efímera. Solo aplica si es exclusiva.</summary>
    protected virtual string? BindingRoutingKey => null;

    protected abstract Task HandleAsync(TEvent message, CancellationToken cancellationToken);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // El worker puede arrancar antes que el broker. Reintentar indefinidamente con backoff es lo
        // correcto para un servicio de fondo: no hay nadie esperando una respuesta.
        var connectionRetry = new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = int.MaxValue,
                Delay = TimeSpan.FromSeconds(2),
                MaxDelay = TimeSpan.FromSeconds(30),
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
            })
            .Build();

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await connectionRetry
                    .ExecuteAsync(async token => await ConsumeAsync(token).ConfigureAwait(false), stoppingToken)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception exception)
            {
                ConsumerRestarting(logger, exception, QueueName);
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken).ConfigureAwait(false);
            }
        }
    }

    private async Task ConsumeAsync(CancellationToken stoppingToken)
    {
        await using var channel = await connectionProvider.AcquireChannelAsync(stoppingToken).ConfigureAwait(false);

        await channel.BasicQosAsync(prefetchSize: 0, PrefetchCount, global: false, stoppingToken)
            .ConfigureAwait(false);

        var queue = await PrepareQueueAsync(channel, stoppingToken).ConfigureAwait(false);

        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += async (_, arguments) =>
            await OnReceivedAsync(channel, arguments, stoppingToken).ConfigureAwait(false);

        await channel.BasicConsumeAsync(queue, autoAck: false, consumer, stoppingToken).ConfigureAwait(false);

        ConsumerStarted(logger, queue);

        // El canal vive mientras no se cancele; sin esta espera el 'await using' lo cerraría de
        // inmediato y el consumidor moriría en silencio nada más registrarse.
        await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken).ConfigureAwait(false);
    }

    /// <summary>Devuelve el nombre de la cola desde la que consumir.</summary>
    private async Task<string> PrepareQueueAsync(IChannel channel, CancellationToken cancellationToken)
    {
        if (!UsesExclusiveQueue)
        {
            return QueueName;
        }

        // Nombre vacío: lo genera el servidor. exclusive + autoDelete hacen que la cola desaparezca
        // al desconectarse la instancia, así que apagar una réplica no deja colas huérfanas
        // acumulando mensajes para siempre.
        var declared = await channel.QueueDeclareAsync(
            queue: string.Empty, durable: false, exclusive: true, autoDelete: true,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        await channel.QueueBindAsync(
            declared.QueueName,
            Topology.TelemetryExchange,
            BindingRoutingKey ?? throw new InvalidOperationException(
                "Una cola exclusiva necesita BindingRoutingKey."),
            cancellationToken: cancellationToken).ConfigureAwait(false);

        return declared.QueueName;
    }

    private async Task OnReceivedAsync(
        IChannel channel,
        BasicDeliverEventArgs arguments,
        CancellationToken stoppingToken)
    {
        try
        {
            var payload = Encoding.UTF8.GetString(arguments.Body.Span);
            var message = JsonSerializer.Deserialize<TEvent>(payload, SerializerOptions);

            if (message is null)
            {
                // Un mensaje que no se puede deserializar no mejora con el tiempo: reencolarlo sería
                // un bucle infinito. Va a la dead-letter queue, donde queda para inspección.
                UndeserializableMessage(logger, QueueName);
                await channel.BasicNackAsync(arguments.DeliveryTag, multiple: false, requeue: false, stoppingToken)
                    .ConfigureAwait(false);
                return;
            }

            await HandleAsync(message, stoppingToken).ConfigureAwait(false);
            await channel.BasicAckAsync(arguments.DeliveryTag, multiple: false, stoppingToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            MessageHandlingFailed(logger, exception, QueueName);

            // requeue: false y dead-letter exchange. Reencolar dejaría el mensaje rebotando contra
            // el mismo fallo a máxima velocidad, consumiendo CPU y ocultando el problema; en la DLQ
            // es visible y se puede reinyectar cuando la causa esté resuelta.
            await channel.BasicNackAsync(arguments.DeliveryTag, multiple: false, requeue: false, stoppingToken)
                .ConfigureAwait(false);
        }
    }

    [LoggerMessage(EventId = 4001, Level = LogLevel.Information, Message = "Consumiendo la cola {Queue}.")]
    private static partial void ConsumerStarted(ILogger logger, string queue);

    [LoggerMessage(EventId = 4002, Level = LogLevel.Warning, Message = "El consumidor de {Queue} se reinicia.")]
    private static partial void ConsumerRestarting(ILogger logger, Exception exception, string queue);

    [LoggerMessage(EventId = 4003, Level = LogLevel.Error,
        Message = "Mensaje no deserializable en {Queue}; enviado a la dead-letter queue.")]
    private static partial void UndeserializableMessage(ILogger logger, string queue);

    [LoggerMessage(EventId = 4004, Level = LogLevel.Error,
        Message = "Fallo al procesar un mensaje de {Queue}; enviado a la dead-letter queue.")]
    private static partial void MessageHandlingFailed(ILogger logger, Exception exception, string queue);
}
