using FleetTelemetry.Application.Abstractions.Messaging;
using FleetTelemetry.Application.Abstractions.Ports;
using FleetTelemetry.Contracts;
using FleetTelemetry.Contracts.Events;
using FleetTelemetry.Domain.Alerts;
using FleetTelemetry.Domain.Common;
using FleetTelemetry.Domain.Telemetry;
using FleetTelemetry.Domain.Vehicles;

namespace FleetTelemetry.Application.Alerts.ProcessPanic;

/// <summary>Persiste la alerta de pánico y la difunde al dashboard.</summary>
public sealed class ProcessPanicHandler(
    IAlertRepository alerts,
    IVehicleRepository vehicles,
    IEventBus eventBus,
    IClock clock) : ICommandHandler<ProcessPanicCommand, ProcessPanicResult>
{
    public async Task<Result<ProcessPanicResult>> HandleAsync(
        ProcessPanicCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var vehicleId = VehicleId.Create(command.VehicleId);
        if (vehicleId.IsFailure)
        {
            return Result.Failure<ProcessPanicResult>(vehicleId.Error);
        }

        var position = Coordinate.Create(command.Latitude, command.Longitude);
        if (position.IsFailure)
        {
            return Result.Failure<ProcessPanicResult>(position.Error);
        }

        var now = clock.UtcNow;

        await vehicles.EnsureRegisteredAsync(vehicleId.Value, now, cancellationToken).ConfigureAwait(false);

        var alert = Alert.Panic(vehicleId.Value, position.Value, command.PressedAt);

        await alerts.AddAsync(alert, cancellationToken).ConfigureAwait(false);
        await alerts.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await eventBus.PublishAsync(
            new AlertRaisedEvent(
                EventId: Guid.CreateVersion7(),
                OccurredAt: now,
                CorrelationId: command.CorrelationId,
                AlertId: alert.Id,
                VehicleId: alert.VehicleId.Value,
                Kind: alert.Kind.ToString(),
                Latitude: alert.Position.Latitude,
                Longitude: alert.Position.Longitude,
                RaisedAt: alert.RaisedAt,
                Detail: alert.Detail),
            Topology.RoutingKeys.AlertRaised,
            cancellationToken).ConfigureAwait(false);

        return Result.Success(new ProcessPanicResult(alert.Id));
    }
}
