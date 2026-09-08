using FleetTelemetry.Application.Abstractions.Ports;
using FleetTelemetry.Domain.Vehicles;
using Microsoft.EntityFrameworkCore;

namespace FleetTelemetry.Infrastructure.Persistence.Repositories;

internal sealed class VehicleRepository(FleetDbContext context) : IVehicleRepository
{
    public Task<Vehicle?> GetAsync(VehicleId vehicleId, CancellationToken cancellationToken) =>
        context.Vehicles.FirstOrDefaultAsync(vehicle => vehicle.Id == vehicleId, cancellationToken);

    public async Task<IReadOnlyList<Vehicle>> ListAsync(bool includeDeleted, CancellationToken cancellationToken)
    {
        var query = context.Vehicles.AsNoTracking();

        if (!includeDeleted)
        {
            query = query.Where(vehicle => vehicle.State != VehicleLifecycleState.Deleted);
        }

        return await query.OrderBy(vehicle => vehicle.Id).ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task AddAsync(Vehicle vehicle, CancellationToken cancellationToken) =>
        await context.Vehicles.AddAsync(vehicle, cancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
    public async Task<Vehicle> EnsureRegisteredAsync(
        VehicleId vehicleId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var existing = await GetAsync(vehicleId, cancellationToken).ConfigureAwait(false);
        if (existing is not null)
        {
            return existing;
        }

        var vehicle = Vehicle.Register(vehicleId, label: null, now);
        await AddAsync(vehicle, cancellationToken).ConfigureAwait(false);

        try
        {
            await SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return vehicle;
        }
        catch (DbUpdateException)
        {
            // Dos mensajes del mismo vehículo nuevo procesados a la vez: ambos ven que no existe y
            // ambos intentan insertarlo. El perdedor recarga en lugar de fallar; la alternativa
            // sería un lock que serializaría todo el alta de vehículos.
            context.ChangeTracker.Clear();
            var winner = await GetAsync(vehicleId, cancellationToken).ConfigureAwait(false);

            if (winner is null)
            {
                // No fue una carrera: el fallo es otro y no debe silenciarse.
                throw;
            }

            return winner;
        }
    }

    public Task RemoveAsync(Vehicle vehicle, CancellationToken cancellationToken)
    {
        context.Vehicles.Remove(vehicle);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken) => context.SaveChangesAsync(cancellationToken);
}
