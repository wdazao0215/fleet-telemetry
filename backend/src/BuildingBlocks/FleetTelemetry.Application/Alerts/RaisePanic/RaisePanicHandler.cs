using FleetTelemetry.Application.Abstractions.Messaging;
using FleetTelemetry.Application.Abstractions.Ports;
using FleetTelemetry.Contracts;
using FleetTelemetry.Contracts.Events;
using FleetTelemetry.Domain.Common;
using FleetTelemetry.Domain.Telemetry;
using FleetTelemetry.Domain.Vehicles;

namespace FleetTelemetry.Application.Alerts.RaisePanic;

/// <summary>
/// Acepta la pulsación del botón de pánico y la encola.
/// </summary>
/// <remarks>
/// No se deduplica: si el conductor pulsa tres veces, las tres llegan. Es una señal de socorro
/// deliberada, no telemetría automática, y filtrarla por parecerse a la anterior sería exactamente
/// el error que no se puede cometer aquí.
/// </remarks>
public sealed class RaisePanicHandler(IEventBus eventBus, IClock clock)
    : ICommandHandler<RaisePanicCommand, RaisePanicResult>
{
    public async Task<Result<RaisePanicResult>> HandleAsync(
        RaisePanicCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var vehicleId = VehicleId.Create(command.VehicleId);
        if (vehicleId.IsFailure)
        {
            return Result.Failure<RaisePanicResult>(vehicleId.Error);
        }

        var position = Coordinate.Create(command.Latitude, command.Longitude);
        if (position.IsFailure)
        {
            return Result.Failure<RaisePanicResult>(position.Error);
        }

        var now = clock.UtcNow;

        await eventBus.PublishAsync(
            new PanicButtonPressedEvent(
                EventId: Guid.CreateVersion7(),
                OccurredAt: now,
                CorrelationId: command.CorrelationId,
                VehicleId: vehicleId.Value.Value,
                Latitude: position.Value.Latitude,
                Longitude: position.Value.Longitude,
                PressedAt: command.PressedAt),
            Topology.RoutingKeys.PanicButtonPressed,
            cancellationToken).ConfigureAwait(false);

        return Result.Success(new RaisePanicResult(vehicleId.Value.Value, now));
    }
}
