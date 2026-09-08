using System.Globalization;
using FleetTelemetry.Application.Abstractions.Models;
using FleetTelemetry.Application.Abstractions.Ports;
using FleetTelemetry.Domain.Telemetry;
using FleetTelemetry.Domain.Vehicles;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace FleetTelemetry.Infrastructure.Persistence.Repositories;

/// <summary>
/// Histórico de posiciones sobre la hypertable de TimescaleDB.
/// </summary>
/// <remarks>
/// No usa el change tracker de EF: las posiciones son una tabla de solo-inserción con volumen alto y
/// sin identidad de negocio que rastrear. Se escribe con SQL parametrizado, que además permite
/// <c>ON CONFLICT DO NOTHING</c>.
///
/// Esa cláusula es la que hace **idempotente** al consumidor. RabbitMQ garantiza entrega
/// at-least-once: ante un reinicio del worker o un ack perdido, el mismo mensaje se reprocesa. Sin
/// idempotencia, el reproceso duplicaría filas y falsearía los recorridos.
/// </remarks>
internal sealed class TimescalePositionRepository(FleetDbContext context) : IPositionRepository
{
    private const string InsertSql = """
        INSERT INTO positions (vehicle_id, recorded_at, latitude, longitude)
        VALUES (@vehicleId, @recordedAt, @latitude, @longitude)
        ON CONFLICT (vehicle_id, recorded_at) DO NOTHING;
        """;

    public async Task SaveAsync(TelemetryReading reading, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(reading);

        await context.Database.ExecuteSqlRawAsync(
            InsertSql,
            [
                new NpgsqlParameter("vehicleId", reading.VehicleId.Value),
                new NpgsqlParameter("recordedAt", reading.RecordedAt.UtcDateTime),
                new NpgsqlParameter("latitude", reading.Position.Latitude),
                new NpgsqlParameter("longitude", reading.Position.Longitude),
            ],
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task SaveBatchAsync(
        IReadOnlyCollection<TelemetryReading> readings,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(readings);

        if (readings.Count == 0)
        {
            return;
        }

        // Un único INSERT con varias tuplas en lugar de N round-trips. Es lo que hace viable la
        // sincronización offline: un conductor que sale de un túnel envía minutos de posiciones de
        // golpe, y varios conductores pueden salir a la vez.
        var values = new List<string>(readings.Count);
        var parameters = new List<NpgsqlParameter>(readings.Count * 4);
        var index = 0;

        foreach (var reading in readings)
        {
            values.Add(string.Create(
                CultureInfo.InvariantCulture,
                $"(@v{index}, @t{index}, @lat{index}, @lng{index})"));

            parameters.Add(new NpgsqlParameter($"v{index}", reading.VehicleId.Value));
            parameters.Add(new NpgsqlParameter($"t{index}", reading.RecordedAt.UtcDateTime));
            parameters.Add(new NpgsqlParameter($"lat{index}", reading.Position.Latitude));
            parameters.Add(new NpgsqlParameter($"lng{index}", reading.Position.Longitude));
            index++;
        }

        var sql = $"""
            INSERT INTO positions (vehicle_id, recorded_at, latitude, longitude)
            VALUES {string.Join(", ", values)}
            ON CONFLICT (vehicle_id, recorded_at) DO NOTHING;
            """;

        await context.Database.ExecuteSqlRawAsync(sql, parameters, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<TrackPoint>> GetTrackAsync(
        VehicleId vehicleId,
        DateTimeOffset from,
        DateTimeOffset to,
        int maxPoints,
        CancellationToken cancellationToken)
    {
        // Se ordena descendente y se limita para acotar lo que viaja al navegador: un vehículo
        // emitiendo cada 2 segundos genera 1.800 puntos por hora, y dibujar eso en el mapa no
        // aporta más información que una muestra.
        const string sql = """
            SELECT latitude, longitude, recorded_at
            FROM positions
            WHERE vehicle_id = @vehicleId
              AND recorded_at BETWEEN @from AND @to
            ORDER BY recorded_at DESC
            LIMIT @limit;
            """;

        var connection = context.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        }

        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.Add(new NpgsqlParameter("vehicleId", vehicleId.Value));
        command.Parameters.Add(new NpgsqlParameter("from", from.UtcDateTime));
        command.Parameters.Add(new NpgsqlParameter("to", to.UtcDateTime));
        command.Parameters.Add(new NpgsqlParameter("limit", maxPoints));

        var points = new List<TrackPoint>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var coordinate = Coordinate.Create(reader.GetDouble(0), reader.GetDouble(1));
            if (coordinate.IsFailure)
            {
                continue;
            }

            var recordedAt = new DateTimeOffset(reader.GetDateTime(2), TimeSpan.Zero);
            points.Add(new TrackPoint(coordinate.Value, recordedAt));
        }

        // Se devuelve en orden cronológico: el mapa dibuja una polilínea y necesita los puntos en
        // el orden en que se recorrieron.
        points.Reverse();
        return points;
    }

    public async Task DeleteHistoryAsync(VehicleId vehicleId, CancellationToken cancellationToken) =>
        await context.Database.ExecuteSqlRawAsync(
            "DELETE FROM positions WHERE vehicle_id = @vehicleId;",
            [new NpgsqlParameter("vehicleId", vehicleId.Value)],
            cancellationToken).ConfigureAwait(false);
}
