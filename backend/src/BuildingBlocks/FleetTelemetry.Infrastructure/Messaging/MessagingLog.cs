using Microsoft.Extensions.Logging;

namespace FleetTelemetry.Infrastructure.Messaging;

/// <summary>
/// Logs de mensajería generados en tiempo de compilación.
/// </summary>
/// <remarks>
/// [LoggerMessage] evita el boxing de los argumentos y la construcción de la plantilla cuando el
/// nivel está desactivado. En la ruta de ingesta, que atiende miles de peticiones por segundo, esa
/// diferencia es medible; además hace que cada evento tenga un EventId estable con el que filtrar
/// en producción.
/// </remarks>
internal static partial class MessagingLog
{
    [LoggerMessage(EventId = 1001, Level = LogLevel.Information,
        Message = "Conexión establecida con RabbitMQ en {Host}:{Port}.")]
    public static partial void BrokerConnected(this ILogger logger, string host, int port);

    [LoggerMessage(EventId = 1002, Level = LogLevel.Information,
        Message = "Topología de RabbitMQ declarada.")]
    public static partial void TopologyDeclared(this ILogger logger);

    [LoggerMessage(EventId = 1003, Level = LogLevel.Warning,
        Message = "No se pudo declarar la topología; se reintentará al publicar.")]
    public static partial void TopologyDeclarationDeferred(this ILogger logger, Exception exception);

    [LoggerMessage(EventId = 1004, Level = LogLevel.Error,
        Message = "Circuito de publicación ABIERTO durante {BreakDuration}. Los eventos van al buffer local.")]
    public static partial void CircuitOpened(this ILogger logger, TimeSpan breakDuration);

    [LoggerMessage(EventId = 1005, Level = LogLevel.Information,
        Message = "Circuito de publicación cerrado: el broker responde de nuevo.")]
    public static partial void CircuitClosed(this ILogger logger);

    [LoggerMessage(EventId = 1006, Level = LogLevel.Warning,
        Message = "Circuito abierto: {EventType} encolado en el buffer local ({Buffered} pendientes).")]
    public static partial void BufferedWhileCircuitOpen(this ILogger logger, string eventType, int buffered);

    [LoggerMessage(EventId = 1007, Level = LogLevel.Warning,
        Message = "Fallo al publicar {EventType}; encolado en el buffer local ({Buffered} pendientes).")]
    public static partial void BufferedAfterFailure(
        this ILogger logger, Exception exception, string eventType, int buffered);

    [LoggerMessage(EventId = 1008, Level = LogLevel.Error,
        Message = "Buffer local lleno: se descartó {EventType}. Descartados en total: {Dropped}.")]
    public static partial void BufferOverflow(this ILogger logger, string eventType, long dropped);

    [LoggerMessage(EventId = 1009, Level = LogLevel.Information,
        Message = "Drenados {Drained} mensajes del buffer local; quedan {Remaining}.")]
    public static partial void BufferDrained(this ILogger logger, int drained, int remaining);

    [LoggerMessage(EventId = 1010, Level = LogLevel.Debug,
        Message = "El drenaje se detiene: el broker aún no responde.")]
    public static partial void DrainPaused(this ILogger logger, Exception exception);

    [LoggerMessage(EventId = 1011, Level = LogLevel.Warning,
        Message = "Drenaje del buffer interrumpido con {Remaining} mensajes pendientes.")]
    public static partial void DrainInterrupted(this ILogger logger, int remaining);
}
