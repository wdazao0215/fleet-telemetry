using FleetTelemetry.Application.Abstractions.Messaging;
using FleetTelemetry.Application.Telemetry.ProcessPosition;
using FleetTelemetry.Contracts;
using FleetTelemetry.Contracts.Events;
using FleetTelemetry.Infrastructure.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FleetTelemetry.Processing.Worker.Consumers;

/// <summary>
/// Consume las posiciones aceptadas y las convierte en histórico, estado y alertas.
/// </summary>
internal sealed partial class PositionProcessingConsumer(
    RabbitMqConnectionProvider connectionProvider,
    IServiceScopeFactory scopeFactory,
    ILogger<PositionProcessingConsumer> logger)
    : RabbitMqConsumerService<PositionAcceptedEvent>(connectionProvider, logger)
{
    protected override string QueueName => Topology.Queues.PositionProcessing;

    protected override async Task HandleAsync(PositionAcceptedEvent message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);

        // Un scope por mensaje: el DbContext es scoped y compartirlo entre mensajes acumularía
        // entidades rastreadas hasta degradar el rendimiento y arrastrar estado entre operaciones
        // que no tienen nada que ver.
        await using var scope = scopeFactory.CreateAsyncScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<ICommandDispatcher>();

        var result = await dispatcher.SendAsync(
            new ProcessPositionCommand(
                message.VehicleId,
                message.Latitude,
                message.Longitude,
                message.RecordedAt,
                message.CorrelationId),
            cancellationToken).ConfigureAwait(false);

        if (result.IsFailure)
        {
            // Se registra y se acusa el mensaje: es un fallo de datos, no de infraestructura, y
            // reintentarlo daría exactamente el mismo resultado.
            PositionRejected(logger, message.VehicleId, result.Error.Code);
            return;
        }

        if (result.Value.AlertRaised)
        {
            StoppedVehicleAlertRaised(logger, message.VehicleId);
        }
    }

    [LoggerMessage(EventId = 5001, Level = LogLevel.Warning,
        Message = "Posición de {VehicleId} descartada por {ErrorCode}.")]
    private static partial void PositionRejected(ILogger logger, string vehicleId, string errorCode);

    [LoggerMessage(EventId = 5002, Level = LogLevel.Information,
        Message = "Alerta de vehículo detenido levantada para {VehicleId}.")]
    private static partial void StoppedVehicleAlertRaised(ILogger logger, string vehicleId);
}
