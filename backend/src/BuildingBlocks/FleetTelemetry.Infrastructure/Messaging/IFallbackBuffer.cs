namespace FleetTelemetry.Infrastructure.Messaging;

internal interface IFallbackBuffer
{
    int Count { get; }

    /// <summary>Total de mensajes descartados por buffer lleno desde que arrancó el proceso.</summary>
    long DroppedCount { get; }

    bool TryEnqueue(PendingMessage message);

    IAsyncEnumerable<PendingMessage> DrainAsync(CancellationToken cancellationToken);
}
