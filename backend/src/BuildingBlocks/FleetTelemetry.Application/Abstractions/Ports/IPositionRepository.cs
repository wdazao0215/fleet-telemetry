using FleetTelemetry.Application.Abstractions.Models;
using FleetTelemetry.Domain.Telemetry;
using FleetTelemetry.Domain.Vehicles;

namespace FleetTelemetry.Application.Abstractions.Ports;

/// <summary>Histórico de posiciones.</summary>
public interface IPositionRepository
{
    Task SaveAsync(TelemetryReading reading, CancellationToken cancellationToken);

    /// <summary>
    /// Persiste varias lecturas en una sola operación.
    /// </summary>
    /// <remarks>
    /// Existe por la sincronización offline: un conductor que sale de un túnel envía diez minutos de
    /// posiciones de golpe. Insertarlas una a una multiplicaría los viajes a la base de datos por el
    /// número de vehículos que salen del túnel a la vez.
    /// </remarks>
    Task SaveBatchAsync(IReadOnlyCollection<TelemetryReading> readings, CancellationToken cancellationToken);

    Task<IReadOnlyList<TrackPoint>> GetTrackAsync(
        VehicleId vehicleId,
        DateTimeOffset from,
        DateTimeOffset to,
        int maxPoints,
        CancellationToken cancellationToken);

    Task DeleteHistoryAsync(VehicleId vehicleId, CancellationToken cancellationToken);
}
