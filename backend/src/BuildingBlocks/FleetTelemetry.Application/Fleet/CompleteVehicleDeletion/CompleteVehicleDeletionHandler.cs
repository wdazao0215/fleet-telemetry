using FleetTelemetry.Application.Abstractions.Messaging;
using FleetTelemetry.Application.Abstractions.Ports;
using FleetTelemetry.Contracts;
using FleetTelemetry.Contracts.Events;
using FleetTelemetry.Domain.Common;
using FleetTelemetry.Domain.Telemetry;
using FleetTelemetry.Domain.Vehicles;

namespace FleetTelemetry.Application.Fleet.CompleteVehicleDeletion;

/// <summary>
/// Segundo paso de la saga: purga los datos del vehículo y consuma la eliminación.
/// </summary>
/// <remarks>
/// El orden de purga no es casual. **Primero la caché**, que es lo que el dashboard lee: así el
/// vehículo desaparece de la pantalla del operador de inmediato, aunque el borrado del histórico
/// —millones de filas— tarde. Al revés, el histórico estaría vacío mientras el mapa sigue mostrando
/// un vehículo que ya no existe.
///
/// Todos los pasos son idempotentes: borrar lo ya borrado no falla. Es imprescindible porque la cola
/// entrega at-least-once y este mensaje puede reprocesarse.
/// </remarks>
public sealed class CompleteVehicleDeletionHandler(
    IVehicleRepository vehicles,
    IPositionRepository positions,
    IAlertRepository alerts,
    IPositionCache cache,
    IEventBus eventBus,
    IClock clock) : ICommandHandler<CompleteVehicleDeletionCommand, CompleteVehicleDeletionResult>
{
    public async Task<Result<CompleteVehicleDeletionResult>> HandleAsync(
        CompleteVehicleDeletionCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var vehicleId = VehicleId.Create(command.VehicleId);
        if (vehicleId.IsFailure)
        {
            return Result.Failure<CompleteVehicleDeletionResult>(vehicleId.Error);
        }

        var vehicle = await vehicles.GetAsync(vehicleId.Value, cancellationToken).ConfigureAwait(false);

        if (vehicle is null)
        {
            // Ya se completó en una entrega anterior del mismo mensaje. No es un error.
            return Result.Success(new CompleteVehicleDeletionResult(command.VehicleId, Succeeded: true, null));
        }

        try
        {
            await cache.PurgeVehicleAsync(vehicleId.Value, cancellationToken).ConfigureAwait(false);
            await alerts.DeleteByVehicleAsync(vehicleId.Value, cancellationToken).ConfigureAwait(false);
            await positions.DeleteHistoryAsync(vehicleId.Value, cancellationToken).ConfigureAwait(false);

            var confirmation = vehicle.ConfirmDeletion();
            if (confirmation.IsFailure)
            {
                return Result.Failure<CompleteVehicleDeletionResult>(confirmation.Error);
            }

            // La fila NO se borra: queda como lápida en estado Deleted. Borrarla físicamente hacía
            // que el vehículo resucitara, porque su dispositivo sigue emitiendo y el alta automática
            // lo volvía a crear como Active segundos después. Sin la fila no hay nada que recuerde
            // que se dio de baja.
            //
            // El histórico y la caché sí se purgan de verdad: la lápida guarda un identificador y un
            // estado, no datos de localización.
            await vehicles.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            await PublishOutcomeAsync(command, succeeded: true, reason: null, cancellationToken)
                .ConfigureAwait(false);

            return Result.Success(new CompleteVehicleDeletionResult(command.VehicleId, Succeeded: true, null));
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // Compensación. No se revierte a Active: el vehículo puede haber perdido ya parte de sus
            // datos, y devolverlo a la operación normal mostraría un estado incoherente. Queda
            // marcado, visible en el dashboard y reintentable.
            vehicle.FailDeletion(exception.Message);
            await vehicles.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            await PublishOutcomeAsync(command, succeeded: false, exception.Message, cancellationToken)
                .ConfigureAwait(false);

            return Result.Success(
                new CompleteVehicleDeletionResult(command.VehicleId, Succeeded: false, exception.Message));
        }
    }

    private async Task PublishOutcomeAsync(
        CompleteVehicleDeletionCommand command,
        bool succeeded,
        string? reason,
        CancellationToken cancellationToken) =>
        await eventBus.PublishAsync(
            new VehicleDeletionCompletedEvent(
                EventId: Guid.CreateVersion7(),
                OccurredAt: clock.UtcNow,
                CorrelationId: command.CorrelationId,
                VehicleId: command.VehicleId,
                Succeeded: succeeded,
                FailureReason: reason),
            Topology.RoutingKeys.VehicleDeletionCompleted,
            cancellationToken).ConfigureAwait(false);
}
