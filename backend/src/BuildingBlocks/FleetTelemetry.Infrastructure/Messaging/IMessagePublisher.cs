namespace FleetTelemetry.Infrastructure.Messaging;

/// <summary>
/// Publicación cruda de un mensaje ya serializado.
/// </summary>
/// <remarks>
/// Existe para que la política de resiliencia se pueda testear sin levantar RabbitMQ: un doble que
/// falla siempre permite comprobar que el circuito se abre y que el mensaje acaba en el buffer, que
/// es el requisito de tolerancia a fallos del enunciado.
/// </remarks>
internal interface IMessagePublisher
{
    Task PublishRawAsync(string routingKey, string payload, string eventType, CancellationToken cancellationToken);
}
