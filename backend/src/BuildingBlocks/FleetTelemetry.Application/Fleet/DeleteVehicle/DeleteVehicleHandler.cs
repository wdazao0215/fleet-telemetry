using FleetTelemetry.Application.Abstractions.Messaging;
using FleetTelemetry.Application.Abstractions.Ports;
using FleetTelemetry.Contracts;
using FleetTelemetry.Contracts.Events;
using FleetTelemetry.Domain.Common;
using FleetTelemetry.Domain.Telemetry;
using FleetTelemetry.Domain.Vehicles;

namespace FleetTelemetry.Application.Fleet.DeleteVehicle;

/// <summary>
/// Primer paso de la saga de eliminación.
/// </summary>
/// <remarks>
/// El vehículo vive en dos almacenes —PostgreSQL y Redis— y no hay transacción que abarque a los
/// dos. En lugar de fingir atomicidad, el borrado se modela como una saga con estado observable:
///
///   1. Aquí: marcar PendingDeletion en una transacción y publicar el evento. A partir de este
///      momento el vehículo deja de aceptar telemetría, así que no puede llegar una posición nueva
///      que resucite lo que el paso 2 está borrando.
///   2. El worker: purgar caché, histórico y alertas, y confirmar.
///   3. Si el paso 2 falla tras agotar reintentos, el vehículo queda en DeletionFailed: visible y
///      reintentable, en lugar de un fantasma a medio borrar que nadie sabe que existe.
///
/// El orden importa. Publicar antes de guardar podría procesar el borrado de un vehículo que la
/// transacción luego revierte; guardar sin publicar dejaría un vehículo bloqueado para siempre.
/// </remarks>
public sealed class DeleteVehicleHandler(
    IVehicleRepository vehicles,
    IEventBus eventBus,
    IClock clock) : ICommandHandler<DeleteVehicleCommand, DeleteVehicleResult>
{
    public async Task<Result<DeleteVehicleResult>> HandleAsync(
        DeleteVehicleCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var vehicleId = VehicleId.Create(command.VehicleId);
        if (vehicleId.IsFailure)
        {
            return Result.Failure<DeleteVehicleResult>(vehicleId.Error);
        }

        var vehicle = await vehicles.GetAsync(vehicleId.Value, cancellationToken).ConfigureAwait(false);
        if (vehicle is null)
        {
            return Result.Failure<DeleteVehicleResult>(TelemetryErrors.VehicleNotFound);
        }

        var now = clock.UtcNow;

        // Un vehículo cuyo borrado ya falló se reintenta en lugar de rechazarse: es exactamente el
        // caso para el que existe ese estado.
        var transition = vehicle.State is VehicleLifecycleState.DeletionFailed
            ? vehicle.RetryDeletion(now)
            : vehicle.RequestDeletion(now);

        if (transition.IsFailure)
        {
            return Result.Failure<DeleteVehicleResult>(transition.Error);
        }

        await vehicles.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await eventBus.PublishAsync(
            new VehicleDeletionRequestedEvent(
                EventId: Guid.CreateVersion7(),
                OccurredAt: now,
                CorrelationId: command.CorrelationId,
                VehicleId: vehicle.Id.Value),
            Topology.RoutingKeys.VehicleDeletionRequested,
            cancellationToken).ConfigureAwait(false);

        return Result.Success(new DeleteVehicleResult(vehicle.Id.Value, Accepted: true, vehicle.State.ToString()));
    }
}
