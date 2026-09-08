using FleetTelemetry.Application.Abstractions.Messaging;
using FleetTelemetry.Application.Abstractions.Models;
using FleetTelemetry.Application.Abstractions.Ports;
using FleetTelemetry.Application.Configuration;
using FleetTelemetry.Contracts;
using FleetTelemetry.Contracts.Events;
using FleetTelemetry.Domain.Alerts;
using FleetTelemetry.Domain.Common;
using FleetTelemetry.Domain.Telemetry;
using FleetTelemetry.Domain.Vehicles;

namespace FleetTelemetry.Application.Telemetry.ProcessPosition;

/// <summary>
/// Persiste la posición, actualiza el estado caliente y levanta la alerta de vehículo detenido.
/// </summary>
/// <remarks>
/// Es el corazón del sistema y se ejecuta por cada mensaje de la cola. El orden de las operaciones
/// está pensado para que un fallo a mitad de camino sea recuperable: primero lo durable —la
/// posición— y después la caché, que siempre se puede reconstruir. Al revés, un fallo entre ambos
/// dejaría un dashboard mostrando datos que no existen en el histórico.
/// </remarks>
public sealed class ProcessPositionHandler(
    IPositionRepository positions,
    IVehicleRepository vehicles,
    IAlertRepository alerts,
    IPositionCache cache,
    IEventBus eventBus,
    IClock clock,
    TelemetryOptions options) : ICommandHandler<ProcessPositionCommand, ProcessPositionResult>
{
    public async Task<Result<ProcessPositionResult>> HandleAsync(
        ProcessPositionCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var now = clock.UtcNow;

        var readingResult = TelemetryReading.Create(
            command.VehicleId,
            command.Latitude,
            command.Longitude,
            command.RecordedAt,
            now,
            options.ValidationPolicy());

        if (readingResult.IsFailure)
        {
            // La ingesta ya validó esto. Llegar aquí significa que el mensaje se fabricó fuera del
            // flujo normal, y reintentarlo daría siempre el mismo resultado: se descarta sin
            // reencolar en lugar de rebotar indefinidamente contra la cola.
            return Result.Failure<ProcessPositionResult>(readingResult.Error);
        }

        var reading = readingResult.Value;

        var vehicle = await vehicles
            .EnsureRegisteredAsync(reading.VehicleId, now, cancellationToken)
            .ConfigureAwait(false);

        if (!vehicle.AcceptsTelemetry)
        {
            // Una posición en vuelo puede llegar después de haberse pedido el borrado. Persistirla
            // resucitaría datos del vehículo que la saga acaba de purgar.
            return Result.Success(new ProcessPositionResult(AlertRaised: false, Skipped: true));
        }

        await positions.SaveAsync(reading, cancellationToken).ConfigureAwait(false);

        var lastMovement = await cache
            .GetLastMovementAsync(reading.VehicleId, cancellationToken)
            .ConfigureAwait(false);

        var evaluation = StoppedVehicleDetector.Evaluate(lastMovement, reading, options.StoppedPolicy());

        var alertRaised = false;

        if (evaluation.IsStopped)
        {
            alertRaised = await TryRaiseStoppedAlertAsync(reading, evaluation, command.CorrelationId, now, cancellationToken)
                .ConfigureAwait(false);
        }

        await cache.SaveLiveStateAsync(
            new LivePosition(reading.VehicleId, reading.Position, reading.RecordedAt, evaluation.LastMovement),
            options.LiveStateTtl,
            cancellationToken).ConfigureAwait(false);

        await PublishStateUpdateAsync(reading, evaluation, alertRaised, command.CorrelationId, now, cancellationToken)
            .ConfigureAwait(false);

        return Result.Success(new ProcessPositionResult(alertRaised, Skipped: false));
    }

    /// <summary>
    /// Anuncia el estado resultante para que el dashboard lo reciba en vivo.
    /// </summary>
    /// <remarks>
    /// Se publica el estado ya derivado y no la posición cruda: si el navegador tuviera que decidir
    /// si el vehículo está detenido, la regla de negocio viviría en dos sitios y se desincronizaría
    /// a la primera.
    /// </remarks>
    private async Task PublishStateUpdateAsync(
        TelemetryReading reading,
        MovementEvaluation evaluation,
        bool alertRaised,
        string correlationId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var status = alertRaised || evaluation.IsStopped
            ? VehicleActivityStatus.Alerted
            : evaluation.HasMoved
                ? VehicleActivityStatus.Moving
                : VehicleActivityStatus.Stopped;

        await eventBus.PublishAsync(
            new VehicleStateUpdatedEvent(
                EventId: Guid.CreateVersion7(),
                OccurredAt: now,
                CorrelationId: correlationId,
                VehicleId: reading.VehicleId.Value,
                Latitude: reading.Position.Latitude,
                Longitude: reading.Position.Longitude,
                RecordedAt: reading.RecordedAt,
                Status: status.ToString(),
                StationarySince: evaluation.HasMoved ? null : evaluation.LastMovement.ObservedAt),
            Topology.RoutingKeys.VehicleStateUpdated,
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<bool> TryRaiseStoppedAlertAsync(
        TelemetryReading reading,
        MovementEvaluation evaluation,
        string correlationId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        // Un vehículo detenido cumple la condición en cada lectura, cada 2-5 segundos. Sin este
        // cooldown, un solo camión aparcado generaría cientos de alertas idénticas y el panel
        // dejaría de ser útil justo cuando más se necesita.
        var shouldRaise = await cache
            .TryMarkAlertRaisedAsync(reading.VehicleId, AlertKind.StoppedVehicle, options.AlertCooldown, cancellationToken)
            .ConfigureAwait(false);

        if (!shouldRaise)
        {
            return false;
        }

        var alert = Alert.StoppedVehicle(reading.VehicleId, reading.Position, now, evaluation.StationaryFor);

        await alerts.AddAsync(alert, cancellationToken).ConfigureAwait(false);
        await alerts.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await eventBus.PublishAsync(
            new AlertRaisedEvent(
                EventId: Guid.CreateVersion7(),
                OccurredAt: now,
                CorrelationId: correlationId,
                AlertId: alert.Id,
                VehicleId: alert.VehicleId.Value,
                Kind: alert.Kind.ToString(),
                Latitude: alert.Position.Latitude,
                Longitude: alert.Position.Longitude,
                RaisedAt: alert.RaisedAt,
                Detail: alert.Detail),
            Topology.RoutingKeys.AlertRaised,
            cancellationToken).ConfigureAwait(false);

        return true;
    }
}
