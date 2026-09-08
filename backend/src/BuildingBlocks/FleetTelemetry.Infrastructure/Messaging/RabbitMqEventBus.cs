using System.Text;
using System.Text.Json;
using FleetTelemetry.Contracts;
using RabbitMQ.Client;

namespace FleetTelemetry.Infrastructure.Messaging;

/// <summary>
/// Publicación directa contra RabbitMQ, sin política de resiliencia.
/// </summary>
/// <remarks>
/// Se mantiene deliberadamente ingenuo: falla si el broker no está. La tolerancia a fallos vive en
/// <see cref="ResilientEventBus"/>, que lo envuelve. Separarlos permite testear la política de
/// resiliencia sin broker y la publicación sin política.
/// </remarks>
internal sealed class RabbitMqEventBus(RabbitMqConnectionProvider connectionProvider) : IMessagePublisher
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public static string Serialize<TEvent>(TEvent integrationEvent)
        where TEvent : IIntegrationEvent =>
        JsonSerializer.Serialize(integrationEvent, SerializerOptions);

    public async Task PublishRawAsync(
        string routingKey,
        string payload,
        string eventType,
        CancellationToken cancellationToken)
    {
        await using var channel = await connectionProvider.AcquireChannelAsync(cancellationToken)
            .ConfigureAwait(false);

        var properties = new BasicProperties
        {
            // Persistente: un reinicio del broker no debe llevarse por delante posiciones aceptadas.
            Persistent = true,
            ContentType = "application/json",
            Type = eventType,
        };

        await channel.BasicPublishAsync(
            exchange: Topology.TelemetryExchange,
            routingKey: routingKey,
            mandatory: false,
            basicProperties: properties,
            body: Encoding.UTF8.GetBytes(payload),
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }
}
