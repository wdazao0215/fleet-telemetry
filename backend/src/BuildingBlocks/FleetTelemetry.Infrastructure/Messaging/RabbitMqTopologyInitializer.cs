using FleetTelemetry.Contracts;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;

namespace FleetTelemetry.Infrastructure.Messaging;

/// <summary>
/// Declara exchanges, colas y bindings al arrancar.
/// </summary>
/// <remarks>
/// La declaración es idempotente, así que la ejecutan todos los servicios: cualquiera de ellos puede
/// arrancar primero y ninguno debe asumir que otro ya preparó el terreno. Las colas llevan
/// dead-letter exchange desde el principio; añadirlo después obliga a borrar y recrear la cola,
/// porque los argumentos de una cola existente no se pueden modificar.
/// </remarks>
public sealed class RabbitMqTopologyInitializer(
    RabbitMqConnectionProvider connectionProvider,
    ILogger<RabbitMqTopologyInitializer> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var channel = await connectionProvider.AcquireChannelAsync(cancellationToken)
                .ConfigureAwait(false);

            await channel.ExchangeDeclareAsync(
                Topology.TelemetryExchange, ExchangeType.Topic, durable: true, autoDelete: false,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            await channel.ExchangeDeclareAsync(
                Topology.DeadLetterExchange, ExchangeType.Fanout, durable: true, autoDelete: false,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            await channel.QueueDeclareAsync(
                Topology.Queues.DeadLetter, durable: true, exclusive: false, autoDelete: false,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            await channel.QueueBindAsync(
                Topology.Queues.DeadLetter, Topology.DeadLetterExchange, routingKey: string.Empty,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            var deadLetterArguments = new Dictionary<string, object?>
            {
                ["x-dead-letter-exchange"] = Topology.DeadLetterExchange,
            };

            await DeclareAndBindAsync(
                channel, Topology.Queues.PositionProcessing, Topology.RoutingKeys.PositionAccepted,
                deadLetterArguments, cancellationToken).ConfigureAwait(false);

            await DeclareAndBindAsync(
                channel, Topology.Queues.VehicleDeletion, Topology.RoutingKeys.VehicleDeletionRequested,
                deadLetterArguments, cancellationToken).ConfigureAwait(false);

            await DeclareAndBindAsync(
                channel, Topology.Queues.AlertFanout, Topology.RoutingKeys.AlertRaised,
                deadLetterArguments, cancellationToken).ConfigureAwait(false);

            await DeclareAndBindAsync(
                channel, Topology.Queues.PanicProcessing, Topology.RoutingKeys.PanicButtonPressed,
                deadLetterArguments, cancellationToken).ConfigureAwait(false);

            logger.TopologyDeclared();
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // Arrancar sin broker es un estado legítimo: el circuit breaker y el buffer local
            // existen justamente para eso. Fallar aquí impediría que la ingesta se levantase.
            logger.TopologyDeclarationDeferred(exception);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private static async Task DeclareAndBindAsync(
        IChannel channel,
        string queue,
        string routingKey,
        Dictionary<string, object?> arguments,
        CancellationToken cancellationToken)
    {
        await channel.QueueDeclareAsync(
            queue, durable: true, exclusive: false, autoDelete: false, arguments,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        await channel.QueueBindAsync(
            queue, Topology.TelemetryExchange, routingKey,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }
}
