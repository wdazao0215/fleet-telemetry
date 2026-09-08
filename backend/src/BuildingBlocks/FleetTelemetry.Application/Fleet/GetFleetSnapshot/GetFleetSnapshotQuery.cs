using FleetTelemetry.Application.Abstractions.Messaging;
using FleetTelemetry.Domain.Vehicles;

namespace FleetTelemetry.Application.Fleet.GetFleetSnapshot;

/// <summary>Estado actual de toda la flota, tal como lo pinta el dashboard.</summary>
public sealed record GetFleetSnapshotQuery : IQuery<IReadOnlyList<VehicleSnapshot>>;

/// <param name="Status">Valor derivado, no almacenado: se calcula en cada consulta.</param>
public sealed record VehicleSnapshot(
    string VehicleId,
    string Label,
    VehicleActivityStatus Status,
    VehicleLifecycleState LifecycleState,
    double? Latitude,
    double? Longitude,
    DateTimeOffset? LastSeenAt,
    DateTimeOffset? StationarySince,
    bool HasUnacknowledgedAlert);
