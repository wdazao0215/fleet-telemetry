using FleetTelemetry.Domain.Vehicles;

namespace FleetTelemetry.Application.Abstractions.Ports;

public interface IVehicleRepository
{
    Task<Vehicle?> GetAsync(VehicleId vehicleId, CancellationToken cancellationToken);

    Task<IReadOnlyList<Vehicle>> ListAsync(bool includeDeleted, CancellationToken cancellationToken);

    Task AddAsync(Vehicle vehicle, CancellationToken cancellationToken);

    /// <summary>
    /// Registra el vehículo si aún no existe y devuelve el que quede vigente.
    /// </summary>
    /// <remarks>
    /// La ingesta acepta vehículos que nunca se dieron de alta explícitamente: en campo, un
    /// dispositivo nuevo empieza a emitir y nadie lo registró antes. Rechazarlo perdería datos
    /// reales por un trámite administrativo.
    /// </remarks>
    Task<Vehicle> EnsureRegisteredAsync(VehicleId vehicleId, DateTimeOffset now, CancellationToken cancellationToken);

    Task RemoveAsync(Vehicle vehicle, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
