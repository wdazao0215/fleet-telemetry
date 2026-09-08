using FleetTelemetry.Application.Abstractions.Messaging;
using FleetTelemetry.Application.Abstractions.Ports;
using FleetTelemetry.Domain.Common;
using FleetTelemetry.Domain.Telemetry;
using FleetTelemetry.Domain.Vehicles;

namespace FleetTelemetry.Application.Fleet.GetVehicleTrack;

public sealed class GetVehicleTrackHandler(IPositionRepository positions)
    : IQueryHandler<GetVehicleTrackQuery, VehicleTrack>
{
    /// <summary>
    /// Tope duro de puntos devueltos.
    /// </summary>
    /// <remarks>
    /// Un vehículo emitiendo cada 2 segundos genera 1.800 puntos por hora. Dibujar 50.000 puntos en
    /// una polilínea no añade información y congela el navegador; el límite protege al cliente de un
    /// rango de fechas demasiado amplio.
    /// </remarks>
    private const int MaxAllowedPoints = 2_000;

    public async Task<Result<VehicleTrack>> HandleAsync(
        GetVehicleTrackQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var vehicleId = VehicleId.Create(query.VehicleId);
        if (vehicleId.IsFailure)
        {
            return Result.Failure<VehicleTrack>(vehicleId.Error);
        }

        if (query.To < query.From)
        {
            return Result.Failure<VehicleTrack>(Error.Validation(
                "track.invalid_range", "El fin del rango no puede ser anterior al inicio."));
        }

        var limit = Math.Clamp(query.MaxPoints, 1, MaxAllowedPoints);

        var points = await positions
            .GetTrackAsync(vehicleId.Value, query.From, query.To, limit, cancellationToken)
            .ConfigureAwait(false);

        return Result.Success(new VehicleTrack(
            vehicleId.Value.Value,
            [.. points.Select(point => new TrackPointDto(
                point.Position.Latitude, point.Position.Longitude, point.RecordedAt))]));
    }
}
