using FleetTelemetry.Domain.Alerts;
using FleetTelemetry.Domain.Vehicles;

namespace FleetTelemetry.Application.Abstractions.Ports;

public interface IAlertRepository
{
    Task AddAsync(Alert alert, CancellationToken cancellationToken);

    Task<Alert?> GetAsync(Guid alertId, CancellationToken cancellationToken);

    Task<IReadOnlyList<Alert>> ListRecentAsync(int limit, bool onlyUnacknowledged, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<VehicleId>> ListVehiclesWithUnacknowledgedAlertsAsync(CancellationToken cancellationToken);

    Task DeleteByVehicleAsync(VehicleId vehicleId, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
