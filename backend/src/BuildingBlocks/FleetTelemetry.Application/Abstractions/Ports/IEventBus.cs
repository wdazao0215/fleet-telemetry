using FleetTelemetry.Contracts;

namespace FleetTelemetry.Application.Abstractions.Ports;

/// <summary>
/// Publicación de eventos de integración hacia los demás servicios.
/// </summary>
/// <remarks>
/// La aplicación no sabe si detrás hay RabbitMQ, SQS o un buffer en memoria. Esa ignorancia es lo
/// que permite que la ingesta siga aceptando datos cuando el broker no responde: el adaptador
/// decide encolar en local, y el caso de uso ni se entera. Ver docs/adr/0004.
/// </remarks>
public interface IEventBus
{
    Task PublishAsync<TEvent>(TEvent integrationEvent, string routingKey, CancellationToken cancellationToken)
        where TEvent : IIntegrationEvent;
}
