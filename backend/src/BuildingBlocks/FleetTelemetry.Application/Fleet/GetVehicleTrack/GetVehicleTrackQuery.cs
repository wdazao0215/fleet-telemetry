using FleetTelemetry.Application.Abstractions.Messaging;

namespace FleetTelemetry.Application.Fleet.GetVehicleTrack;

/// <summary>Recorrido histórico de un vehículo en una ventana de tiempo.</summary>
public sealed record GetVehicleTrackQuery(
    string VehicleId,
    DateTimeOffset From,
    DateTimeOffset To,
    int MaxPoints = 500) : IQuery<VehicleTrack>;

public sealed record VehicleTrack(string VehicleId, IReadOnlyList<TrackPointDto> Points);

public sealed record TrackPointDto(double Latitude, double Longitude, DateTimeOffset RecordedAt);
