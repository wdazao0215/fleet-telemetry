using FleetTelemetry.Application.Abstractions.Messaging;
using FleetTelemetry.Application.Abstractions.Ports;
using FleetTelemetry.Application.Configuration;
using FleetTelemetry.Contracts;
using FleetTelemetry.Contracts.Events;
using FleetTelemetry.Domain.Common;
using FleetTelemetry.Domain.Telemetry;

namespace FleetTelemetry.Application.Telemetry.IngestPosition;

/// <summary>
/// Valida la lectura, descarta reenvíos y la publica para su procesamiento asíncrono.
/// </summary>
/// <remarks>
/// El handler no persiste ni calcula alertas. La ingesta debe responder rápido y sobrevivir a la
/// caída de la base de datos; todo lo que puede esperar viaja por la cola hasta el worker.
/// </remarks>
public sealed class IngestPositionHandler(
    IPositionCache cache,
    IEventBus eventBus,
    IClock clock,
    TelemetryOptions options) : ICommandHandler<IngestPositionCommand, IngestPositionResult>
{
    public async Task<Result<IngestPositionResult>> HandleAsync(
        IngestPositionCommand command,
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
            return Result.Failure<IngestPositionResult>(readingResult.Error);
        }

        var reading = readingResult.Value;

        var isNew = await cache
            .TryRegisterUniqueReadingAsync(reading, options.DeduplicationWindow, cancellationToken)
            .ConfigureAwait(false);

        if (!isNew)
        {
            return Result.Success(new IngestPositionResult(reading.VehicleId.Value, Duplicate: true, now));
        }

        var accepted = new PositionAcceptedEvent(
            EventId: Guid.CreateVersion7(),
            OccurredAt: now,
            CorrelationId: command.CorrelationId,
            VehicleId: reading.VehicleId.Value,
            Latitude: reading.Position.Latitude,
            Longitude: reading.Position.Longitude,
            RecordedAt: reading.RecordedAt);

        // Si el broker está caído, el adaptador resiliente encola en local y devuelve normalmente:
        // la ingesta no puede fallar por un problema aguas abajo. Ver docs/adr/0004.
        await eventBus
            .PublishAsync(accepted, Topology.RoutingKeys.PositionAccepted, cancellationToken)
            .ConfigureAwait(false);

        return Result.Success(new IngestPositionResult(reading.VehicleId.Value, Duplicate: false, now));
    }
}
