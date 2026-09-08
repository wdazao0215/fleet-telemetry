using FleetTelemetry.Application.Abstractions.Ports;
using FleetTelemetry.Domain.Alerts;
using FleetTelemetry.Domain.Vehicles;
using Microsoft.EntityFrameworkCore;

namespace FleetTelemetry.Infrastructure.Persistence.Repositories;

internal sealed class AlertRepository(FleetDbContext context) : IAlertRepository
{
    public async Task AddAsync(Alert alert, CancellationToken cancellationToken) =>
        await context.Alerts.AddAsync(alert, cancellationToken).ConfigureAwait(false);

    public Task<Alert?> GetAsync(Guid alertId, CancellationToken cancellationToken) =>
        context.Alerts.FirstOrDefaultAsync(alert => alert.Id == alertId, cancellationToken);

    public async Task<IReadOnlyList<Alert>> ListRecentAsync(
        int limit,
        bool onlyUnacknowledged,
        CancellationToken cancellationToken)
    {
        var query = context.Alerts.AsNoTracking();

        if (onlyUnacknowledged)
        {
            query = query.Where(alert => alert.AcknowledgedAt == null);
        }

        return await query
            .OrderByDescending(alert => alert.RaisedAt)
            .Take(limit)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyCollection<VehicleId>> ListVehiclesWithUnacknowledgedAlertsAsync(
        CancellationToken cancellationToken) =>
        await context.Alerts
            .AsNoTracking()
            .Where(alert => alert.AcknowledgedAt == null)
            .Select(alert => alert.VehicleId)
            .Distinct()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task DeleteByVehicleAsync(VehicleId vehicleId, CancellationToken cancellationToken) =>
        await context.Alerts
            .Where(alert => alert.VehicleId == vehicleId)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);

    public Task SaveChangesAsync(CancellationToken cancellationToken) => context.SaveChangesAsync(cancellationToken);
}
