using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace FleetTelemetry.Infrastructure.Messaging;

/// <summary>
/// Mantiene una única conexión al broker y la reconstruye cuando se pierde.
/// </summary>
/// <remarks>
/// Una conexión AMQP es cara y está pensada para compartirse; abrir una por publicación agotaría los
/// descriptores de fichero bajo la carga de ingesta. El semáforo evita que, al caerse el broker,
/// cientos de peticiones concurrentes intenten reconectar a la vez.
/// </remarks>
public sealed class RabbitMqConnectionProvider(
    IOptions<RabbitMqOptions> options,
    ILogger<RabbitMqConnectionProvider> logger) : IAsyncDisposable
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly RabbitMqOptions settings = options.Value;
    private IConnection? connection;

    public async Task<IChannel> AcquireChannelAsync(CancellationToken cancellationToken)
    {
        var active = await EnsureConnectionAsync(cancellationToken).ConfigureAwait(false);
        return await active.CreateChannelAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    public bool IsConnected => connection?.IsOpen == true;

    private async Task<IConnection> EnsureConnectionAsync(CancellationToken cancellationToken)
    {
        if (connection is { IsOpen: true })
        {
            return connection;
        }

        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (connection is { IsOpen: true })
            {
                return connection;
            }

            if (connection is not null)
            {
                await connection.DisposeAsync().ConfigureAwait(false);
            }

            var factory = new ConnectionFactory
            {
                HostName = settings.Host,
                Port = settings.Port,
                UserName = settings.Username,
                Password = settings.Password,
                VirtualHost = settings.VirtualHost,
                AutomaticRecoveryEnabled = true,
                TopologyRecoveryEnabled = true,
            };

            connection = await factory.CreateConnectionAsync(cancellationToken).ConfigureAwait(false);
            logger.BrokerConnected(settings.Host, settings.Port);
            return connection;
        }
        finally
        {
            gate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (connection is not null)
        {
            await connection.DisposeAsync().ConfigureAwait(false);
        }

        gate.Dispose();
    }
}
