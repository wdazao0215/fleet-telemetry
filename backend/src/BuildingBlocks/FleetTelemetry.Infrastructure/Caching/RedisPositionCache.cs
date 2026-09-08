using System.Text.Json;
using FleetTelemetry.Application.Abstractions.Models;
using FleetTelemetry.Application.Abstractions.Ports;
using FleetTelemetry.Domain.Alerts;
using FleetTelemetry.Domain.Telemetry;
using FleetTelemetry.Domain.Vehicles;
using StackExchange.Redis;

namespace FleetTelemetry.Infrastructure.Caching;

/// <summary>
/// Estado caliente de la flota sobre Redis.
/// </summary>
public sealed class RedisPositionCache(IConnectionMultiplexer connection) : IPositionCache
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private IDatabase Database => connection.GetDatabase();

    /// <inheritdoc />
    public async Task<bool> TryRegisterUniqueReadingAsync(
        TelemetryReading reading,
        TimeSpan deduplicationWindow,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(reading);
        cancellationToken.ThrowIfCancellationRequested();

        // SET NX es atómico: comprobar y registrar en dos pasos dejaría pasar dos peticiones
        // simultáneas con el mismo paquete, que es exactamente el caso que hay que descartar.
        return await Database
            .StringSetAsync(
                RedisKeys.Deduplication(reading),
                value: string.Empty,
                expiry: deduplicationWindow,
                when: When.NotExists)
            .ConfigureAwait(false);
    }

    public async Task<MovementSnapshot?> GetLastMovementAsync(
        VehicleId vehicleId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var raw = await Database.StringGetAsync(RedisKeys.MovementAnchor(vehicleId)).ConfigureAwait(false);
        if (raw.IsNullOrEmpty)
        {
            return null;
        }

        var document = JsonSerializer.Deserialize<MovementDocument>(raw.ToString(), SerializerOptions);
        if (document is null)
        {
            return null;
        }

        var coordinate = Coordinate.Create(document.Latitude, document.Longitude);

        // Una coordenada inválida en caché solo puede venir de datos corruptos o de un cambio de
        // formato. Se trata como "sin ancla" en lugar de propagar el fallo: el vehículo perderá un
        // ciclo de detección, no el servicio entero.
        return coordinate.IsFailure ? null : new MovementSnapshot(coordinate.Value, document.ObservedAt);
    }

    public async Task SaveLiveStateAsync(
        LivePosition livePosition,
        TimeSpan timeToLive,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(livePosition);
        cancellationToken.ThrowIfCancellationRequested();

        var document = new LiveStateDocument(
            livePosition.VehicleId.Value,
            livePosition.Position.Latitude,
            livePosition.Position.Longitude,
            livePosition.RecordedAt,
            livePosition.LastMovement?.Position.Latitude,
            livePosition.LastMovement?.Position.Longitude,
            livePosition.LastMovement?.ObservedAt);

        var batch = Database.CreateBatch();

        var tasks = new List<Task>
        {
            batch.StringSetAsync(
                RedisKeys.LiveState(livePosition.VehicleId),
                JsonSerializer.Serialize(document, SerializerOptions),
                timeToLive),
            batch.SetAddAsync(RedisKeys.LiveIndex, livePosition.VehicleId.Value),
        };

        if (livePosition.LastMovement is not null)
        {
            var movement = new MovementDocument(
                livePosition.LastMovement.Position.Latitude,
                livePosition.LastMovement.Position.Longitude,
                livePosition.LastMovement.ObservedAt);

            // El ancla vive más que el estado: si caducara mientras el vehículo sigue parado, el
            // cronómetro de detención se reiniciaría y la alerta se retrasaría un ciclo entero.
            tasks.Add(batch.StringSetAsync(
                RedisKeys.MovementAnchor(livePosition.VehicleId),
                JsonSerializer.Serialize(movement, SerializerOptions),
                MovementAnchorTtl(timeToLive)));
        }

        batch.Execute();
        await Task.WhenAll(tasks).ConfigureAwait(false);
    }

    public async Task<IReadOnlyCollection<LivePosition>> GetLiveStateAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var members = await Database.SetMembersAsync(RedisKeys.LiveIndex).ConfigureAwait(false);
        if (members.Length == 0)
        {
            return [];
        }

        var keys = new List<RedisKey>(members.Length);
        var vehicleIds = new List<VehicleId>(members.Length);

        foreach (var member in members)
        {
            var vehicleId = VehicleId.Create(member.ToString());
            if (vehicleId.IsFailure)
            {
                continue;
            }

            vehicleIds.Add(vehicleId.Value);
            keys.Add(RedisKeys.LiveState(vehicleId.Value));
        }

        // Un solo MGET en lugar de N lecturas: el dashboard consulta esto en cada refresco.
        var values = await Database.StringGetAsync([.. keys]).ConfigureAwait(false);
        var results = new List<LivePosition>(values.Length);
        var expired = new List<RedisValue>();

        for (var index = 0; index < values.Length; index++)
        {
            if (values[index].IsNullOrEmpty)
            {
                // El estado caducó pero el índice sigue nombrando al vehículo. Se limpia aquí para
                // que el conjunto no crezca indefinidamente con vehículos que ya no emiten.
                expired.Add(vehicleIds[index].Value);
                continue;
            }

            var position = Deserialize(values[index].ToString());
            if (position is not null)
            {
                results.Add(position);
            }
        }

        if (expired.Count > 0)
        {
            await Database.SetRemoveAsync(RedisKeys.LiveIndex, [.. expired]).ConfigureAwait(false);
        }

        return results;
    }

    public async Task<LivePosition?> GetLiveStateAsync(VehicleId vehicleId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var raw = await Database.StringGetAsync(RedisKeys.LiveState(vehicleId)).ConfigureAwait(false);
        return raw.IsNullOrEmpty ? null : Deserialize(raw.ToString());
    }

    public async Task<bool> TryMarkAlertRaisedAsync(
        VehicleId vehicleId,
        AlertKind kind,
        TimeSpan cooldown,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return await Database
            .StringSetAsync(
                RedisKeys.AlertCooldown(vehicleId, kind),
                value: string.Empty,
                expiry: cooldown,
                when: When.NotExists)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task PurgeVehicleAsync(VehicleId vehicleId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // Las claves de deduplicación no se borran: son efímeras (segundos) y buscarlas exigiría un
        // SCAN sobre todo el espacio de claves. Caducan solas mucho antes de que importen.
        var keys = new List<RedisKey>
        {
            RedisKeys.LiveState(vehicleId),
            RedisKeys.MovementAnchor(vehicleId),
        };

        foreach (var kind in Enum.GetValues<AlertKind>())
        {
            keys.Add(RedisKeys.AlertCooldown(vehicleId, kind));
        }

        await Database.KeyDeleteAsync([.. keys]).ConfigureAwait(false);
        await Database.SetRemoveAsync(RedisKeys.LiveIndex, vehicleId.Value).ConfigureAwait(false);
    }

    private static TimeSpan MovementAnchorTtl(TimeSpan liveStateTtl) =>
        liveStateTtl < TimeSpan.FromHours(6) ? TimeSpan.FromHours(6) : liveStateTtl;

    private static LivePosition? Deserialize(string payload)
    {
        var document = JsonSerializer.Deserialize<LiveStateDocument>(payload, SerializerOptions);
        if (document is null)
        {
            return null;
        }

        var vehicleId = VehicleId.Create(document.VehicleId);
        var position = Coordinate.Create(document.Latitude, document.Longitude);

        if (vehicleId.IsFailure || position.IsFailure)
        {
            return null;
        }

        MovementSnapshot? movement = null;
        if (document.MovementLatitude is { } latitude &&
            document.MovementLongitude is { } longitude &&
            document.MovementObservedAt is { } observedAt)
        {
            var movementPosition = Coordinate.Create(latitude, longitude);
            if (movementPosition.IsSuccess)
            {
                movement = new MovementSnapshot(movementPosition.Value, observedAt);
            }
        }

        return new LivePosition(vehicleId.Value, position.Value, document.RecordedAt, movement);
    }
}
