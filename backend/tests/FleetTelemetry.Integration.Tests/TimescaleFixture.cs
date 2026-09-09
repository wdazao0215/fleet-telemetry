using FleetTelemetry.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Testcontainers.PostgreSql;

namespace FleetTelemetry.Integration.Tests;

/// <summary>
/// TimescaleDB real, con el mismo esquema que aplica el migrador.
/// </summary>
/// <remarks>
/// La imagen es la de TimescaleDB y no la de Postgres a secas porque el esquema llama a
/// <c>create_hypertable</c>: contra un Postgres normal el script fallaría, y el test estaría
/// validando una tabla que no se parece a la de producción.
/// </remarks>
public sealed class TimescaleFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer container =
        new PostgreSqlBuilder("timescale/timescaledb:2.17.2-pg17")
            .WithDatabase("fleet_telemetry")
            .WithUsername("fleet")
            .WithPassword("fleet_test")
            .Build();

    public string ConnectionString => container.GetConnectionString();

    public async Task InitializeAsync()
    {
        await container.StartAsync();

        var script = await File.ReadAllTextAsync(
            Path.Combine(AppContext.BaseDirectory, "Scripts", "001_initial_schema.sql"));

        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(script, connection);
        await command.ExecuteNonQueryAsync();
    }

    public FleetDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<FleetDbContext>().UseNpgsql(ConnectionString).Options);

    public async Task DisposeAsync() => await container.DisposeAsync();
}

[CollectionDefinition(nameof(SharedTimescale))]
public sealed class SharedTimescale : ICollectionFixture<TimescaleFixture>;
