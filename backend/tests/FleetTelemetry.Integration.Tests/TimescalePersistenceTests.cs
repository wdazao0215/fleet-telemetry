using FleetTelemetry.Domain.Telemetry;
using FleetTelemetry.Domain.Vehicles;
using FleetTelemetry.Infrastructure.Persistence.Repositories;
using Npgsql;
using Shouldly;

namespace FleetTelemetry.Integration.Tests;

/// <summary>
/// Persistencia de posiciones contra TimescaleDB real.
/// </summary>
/// <remarks>
/// Aquí se comprueba lo que ningún mock puede: que la hypertable existe, que la clave primaria
/// compuesta se comporta como se espera y que el <c>ON CONFLICT DO NOTHING</c> hace idempotente al
/// consumidor. La cola entrega at-least-once, así que reprocesar un mensaje es normal, no
/// excepcional.
/// </remarks>
[Collection(nameof(SharedTimescale))]
public class TimescalePersistenceTests(TimescaleFixture timescale)
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task PositionsTable_IsAHypertable()
    {
        // Si esto falla, el esquema se creó como tabla normal: seguiría funcionando, pero sin
        // particionado por tiempo ni compresión, y el problema no aparecería hasta tener millones
        // de filas en producción.
        await using var connection = new NpgsqlConnection(timescale.ConnectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(
            "SELECT count(*) FROM timescaledb_information.hypertables WHERE hypertable_name = 'positions';",
            connection);

        var hypertables = (long)(await command.ExecuteScalarAsync())!;

        hypertables.ShouldBe(1);
    }

    [Fact]
    public async Task SaveAsync_TheSameReadingTwice_StoresItOnce()
    {
        // Idempotencia del consumidor. Sin ella, un reinicio del worker duplicaría el recorrido.
        await using var context = timescale.CreateContext();
        var repository = new TimescalePositionRepository(context);
        var reading = Reading("VH-IDEMPOTENT", 4.7110, -74.0721, Now);

        await repository.SaveAsync(reading, CancellationToken.None);
        await repository.SaveAsync(reading, CancellationToken.None);

        var stored = await CountPositionsAsync("VH-IDEMPOTENT");

        stored.ShouldBe(1);
    }

    [Fact]
    public async Task SaveBatchAsync_WithRepeatedReadings_StoresEachOnlyOnce()
    {
        // El lote de sincronización offline puede reenviarse entero si el ack se pierde.
        await using var context = timescale.CreateContext();
        var repository = new TimescalePositionRepository(context);

        var batch = Enumerable.Range(0, 20)
            .Select(index => Reading("VH-BATCH", 4.71 + (index * 0.001), -74.07, Now.AddSeconds(index)))
            .ToArray();

        await repository.SaveBatchAsync(batch, CancellationToken.None);
        await repository.SaveBatchAsync(batch, CancellationToken.None);

        (await CountPositionsAsync("VH-BATCH")).ShouldBe(20);
    }

    [Fact]
    public async Task GetTrackAsync_ReturnsThePointsInChronologicalOrder()
    {
        // El mapa dibuja una polilínea: en otro orden, el recorrido saldría como una maraña.
        await using var context = timescale.CreateContext();
        var repository = new TimescalePositionRepository(context);

        var readings = Enumerable.Range(0, 5)
            .Select(index => Reading("VH-TRACK", 4.71 + (index * 0.001), -74.07, Now.AddSeconds(index * 10)))
            .ToArray();

        await repository.SaveBatchAsync(readings, CancellationToken.None);

        var track = await repository.GetTrackAsync(
            VehicleId.Create("VH-TRACK").Value, Now.AddHours(-1), Now.AddHours(1), 100, CancellationToken.None);

        track.Count.ShouldBe(5);
        track.Select(point => point.RecordedAt).ShouldBeInOrder();
    }

    [Fact]
    public async Task GetTrackAsync_RespectsTheLimitAndKeepsTheMostRecentPoints()
    {
        // Con un rango amplio, importa que se devuelvan los puntos recientes y no los primeros que
        // encuentre el planner: el operador está mirando dónde estuvo el vehículo hace poco.
        await using var context = timescale.CreateContext();
        var repository = new TimescalePositionRepository(context);

        var readings = Enumerable.Range(0, 30)
            .Select(index => Reading("VH-LIMIT", 4.71 + (index * 0.001), -74.07, Now.AddSeconds(index)))
            .ToArray();

        await repository.SaveBatchAsync(readings, CancellationToken.None);

        var track = await repository.GetTrackAsync(
            VehicleId.Create("VH-LIMIT").Value, Now.AddHours(-1), Now.AddHours(1), 10, CancellationToken.None);

        track.Count.ShouldBe(10);
        track[^1].RecordedAt.ShouldBe(Now.AddSeconds(29));
    }

    [Fact]
    public async Task DeleteHistoryAsync_RemovesEveryPositionOfThatVehicleOnly()
    {
        // Paso de la saga de eliminación: debe borrar todo lo del vehículo y nada de los demás.
        await using var context = timescale.CreateContext();
        var repository = new TimescalePositionRepository(context);

        await repository.SaveBatchAsync(
            [Reading("VH-DELETE", 4.71, -74.07, Now), Reading("VH-KEEP", 4.72, -74.08, Now)],
            CancellationToken.None);

        await repository.DeleteHistoryAsync(VehicleId.Create("VH-DELETE").Value, CancellationToken.None);

        (await CountPositionsAsync("VH-DELETE")).ShouldBe(0);
        (await CountPositionsAsync("VH-KEEP")).ShouldBe(1);
    }

    private async Task<long> CountPositionsAsync(string vehicleId)
    {
        await using var connection = new NpgsqlConnection(timescale.ConnectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(
            "SELECT count(*) FROM positions WHERE vehicle_id = @vehicleId;", connection);
        command.Parameters.Add(new NpgsqlParameter("vehicleId", vehicleId));

        return (long)(await command.ExecuteScalarAsync())!;
    }

    private static TelemetryReading Reading(string vehicleId, double latitude, double longitude, DateTimeOffset at) =>
        TelemetryReading.Create(vehicleId, latitude, longitude, at, at, TelemetryValidationPolicy.Default).Value;
}
