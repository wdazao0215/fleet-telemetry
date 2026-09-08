using FleetTelemetry.Application.Abstractions.Messaging;
using FleetTelemetry.Application.Abstractions.Models;
using FleetTelemetry.Application.Abstractions.Ports;
using FleetTelemetry.Application.Configuration;
using FleetTelemetry.Domain.Common;
using FleetTelemetry.Domain.Vehicles;

namespace FleetTelemetry.Application.Fleet.GetFleetSnapshot;

/// <summary>
/// Combina el censo de vehículos (base de datos) con su posición actual (caché).
/// </summary>
/// <remarks>
/// Las posiciones se leen de Redis y no de la hypertable a propósito: servir "dónde está ahora la
/// flota" desde el histórico obligaría a un DISTINCT ON sobre millones de filas en cada refresco del
/// dashboard. La caché responde con una lectura por clave.
/// </remarks>
public sealed class GetFleetSnapshotHandler(
    IVehicleRepository vehicles,
    IAlertRepository alerts,
    IPositionCache cache,
    IClock clock,
    TelemetryOptions options) : IQueryHandler<GetFleetSnapshotQuery, IReadOnlyList<VehicleSnapshot>>
{
    public async Task<Result<IReadOnlyList<VehicleSnapshot>>> HandleAsync(
        GetFleetSnapshotQuery query,
        CancellationToken cancellationToken)
    {
        var registered = await vehicles.ListAsync(includeDeleted: false, cancellationToken).ConfigureAwait(false);
        var livePositions = await cache.GetLiveStateAsync(cancellationToken).ConfigureAwait(false);
        var alerted = await alerts
            .ListVehiclesWithUnacknowledgedAlertsAsync(cancellationToken)
            .ConfigureAwait(false);

        var positionsById = livePositions.ToDictionary(position => position.VehicleId);
        var alertedIds = alerted.ToHashSet();

        var now = clock.UtcNow;
        var stoppedPolicy = options.StoppedPolicy();

        var snapshots = new List<VehicleSnapshot>(registered.Count);

        foreach (var vehicle in registered)
        {
            positionsById.TryGetValue(vehicle.Id, out var live);
            var hasAlert = alertedIds.Contains(vehicle.Id);

            var status = VehicleActivityEvaluator.Resolve(
                live?.RecordedAt,
                live?.LastMovement,
                hasAlert,
                now,
                stoppedPolicy);

            snapshots.Add(new VehicleSnapshot(
                vehicle.Id.Value,
                vehicle.Label,
                status,
                vehicle.State,
                live?.Position.Latitude,
                live?.Position.Longitude,
                live?.RecordedAt,
                StationarySince(live),
                hasAlert));
        }

        return Result.Success<IReadOnlyList<VehicleSnapshot>>(snapshots);
    }

    /// <summary>Desde cuándo no se mueve, para poder mostrar un contador en el panel.</summary>
    private static DateTimeOffset? StationarySince(LivePosition? live) =>
        live?.LastMovement is { } movement && movement.ObservedAt < live.RecordedAt
            ? movement.ObservedAt
            : null;
}
