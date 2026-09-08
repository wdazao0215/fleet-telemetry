using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FleetTelemetry.Infrastructure.Messaging;

/// <summary>
/// Buffer en memoria que sostiene la ingesta mientras el broker está caído.
/// </summary>
/// <remarks>
/// Es deliberadamente un buffer en memoria y no una tabla outbox: la base de datos puede ser
/// justamente lo que está caído, y un fallback que depende del componente que falló no es un
/// fallback. La contrapartida —los mensajes se pierden si el proceso muere con el buffer lleno— se
/// documenta en el README dentro de "Desafíos y Soluciones", junto con la alternativa para
/// producción, que es un outbox persistente en disco local.
/// </remarks>
internal sealed class ChannelFallbackBuffer : IFallbackBuffer
{
    private readonly Channel<PendingMessage> channel;
    private readonly ILogger<ChannelFallbackBuffer> logger;
    private long dropped;
    private int count;

    public ChannelFallbackBuffer(IOptions<RabbitMqOptions> options, ILogger<ChannelFallbackBuffer> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        this.logger = logger;

        // DropOldest: ante una caída prolongada, las posiciones recientes valen más que las viejas.
        // Un vehículo se localiza con su última posición, no con la de hace veinte minutos.
        channel = Channel.CreateBounded<PendingMessage>(
            new BoundedChannelOptions(options.Value.FallbackBufferCapacity)
            {
                FullMode = BoundedChannelFullMode.DropOldest,
                SingleReader = true,
            },
            itemDropped: _ =>
            {
                Interlocked.Increment(ref dropped);
                Interlocked.Decrement(ref count);
            });
    }

    public int Count => Volatile.Read(ref count);

    public long DroppedCount => Interlocked.Read(ref dropped);

    public bool TryEnqueue(PendingMessage message)
    {
        if (!channel.Writer.TryWrite(message))
        {
            Interlocked.Increment(ref dropped);
            return false;
        }

        Interlocked.Increment(ref count);
        return true;
    }

    public async IAsyncEnumerable<PendingMessage> DrainAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        while (channel.Reader.TryRead(out var message))
        {
            Interlocked.Decrement(ref count);
            yield return message;

            if (cancellationToken.IsCancellationRequested)
            {
                logger.DrainInterrupted(Count);
                yield break;
            }
        }

        await Task.CompletedTask.ConfigureAwait(false);
    }
}
