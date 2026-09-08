namespace FleetTelemetry.Contracts;

/// <summary>
/// Mensaje que cruza la frontera entre servicios.
/// </summary>
/// <remarks>
/// Vive en su propio proyecto sin dependencias porque es el contrato público del sistema: el worker
/// y las APIs lo comparten, y un cambio aquí es un cambio de versión del protocolo. Separarlo del
/// dominio evita que un refactor interno rompa a un consumidor desplegado.
/// </remarks>
public interface IIntegrationEvent
{
    /// <summary>Identificador del mensaje, usado para idempotencia en el consumidor.</summary>
    Guid EventId { get; }

    DateTimeOffset OccurredAt { get; }

    /// <summary>Hilo conductor para seguir una posición a través de los tres servicios en los logs.</summary>
    string CorrelationId { get; }
}
