using StackExchange.Redis;
using Testcontainers.Redis;

namespace FleetTelemetry.Integration.Tests;

/// <summary>
/// Redis real en un contenedor efímero.
/// </summary>
/// <remarks>
/// La deduplicación se apoya en la atomicidad de <c>SET NX</c> y en cómo Redis trata los TTL. Un
/// doble de prueba devolvería lo que se le diga y no probaría nada de eso: justamente el
/// comportamiento del que depende que el sistema funcione.
/// </remarks>
public sealed class RedisFixture : IAsyncLifetime
{
    // Misma versión que levanta docker-compose: probar contra otra dejaría fuera justo las
    // diferencias de comportamiento que este test existe para detectar.
    private readonly RedisContainer container = new RedisBuilder("redis:7.4-alpine").Build();

    public IConnectionMultiplexer Connection { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await container.StartAsync();
        Connection = await ConnectionMultiplexer.ConnectAsync(container.GetConnectionString());
    }

    public async Task DisposeAsync()
    {
        Connection.Dispose();
        await container.DisposeAsync();
    }
}

/// <summary>Comparte un único contenedor entre todos los tests que lo necesitan.</summary>
/// <remarks>Levantar Redis por test multiplicaría el tiempo de la suite sin aislar nada: cada test
/// usa identificadores de vehículo distintos.</remarks>
[CollectionDefinition(nameof(SharedRedis))]
public sealed class SharedRedis : ICollectionFixture<RedisFixture>;
