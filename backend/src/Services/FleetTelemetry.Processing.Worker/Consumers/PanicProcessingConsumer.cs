using FleetTelemetry.Application.Abstractions.Messaging;
using FleetTelemetry.Application.Alerts.ProcessPanic;
using FleetTelemetry.Contracts;
using FleetTelemetry.Contracts.Events;
using FleetTelemetry.Infrastructure.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FleetTelemetry.Processing.Worker.Consumers;

internal sealed partial class PanicProcessingConsumer(
    RabbitMqConnectionProvider connectionProvider,
    IServiceScopeFactory scopeFactory,
    ILogger<PanicProcessingConsumer> logger)
    : RabbitMqConsumerService<PanicButtonPressedEvent>(connectionProvider, logger)
{
    protected override string QueueName => Topology.Queues.PanicProcessing;

    /// <summary>
    /// Prefetch de 1: una alerta de pánico se procesa y se acusa antes de aceptar la siguiente.
    /// </summary>
    /// <remarks>
    /// El volumen es mínimo y la prioridad máxima; no tiene sentido acumular señales de socorro en
    /// memoria del consumidor esperando turno.
    /// </remarks>
    protected override ushort PrefetchCount => 1;

    protected override async Task HandleAsync(PanicButtonPressedEvent message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);

        await using var scope = scopeFactory.CreateAsyncScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<ICommandDispatcher>();

        var result = await dispatcher.SendAsync(
            new ProcessPanicCommand(
                message.VehicleId,
                message.Latitude,
                message.Longitude,
                message.PressedAt,
                message.CorrelationId),
            cancellationToken).ConfigureAwait(false);

        if (result.IsFailure)
        {
            PanicRejected(logger, message.VehicleId, result.Error.Code);
            return;
        }

        PanicRegistered(logger, message.VehicleId);
    }

    [LoggerMessage(EventId = 5003, Level = LogLevel.Error,
        Message = "Alerta de pánico de {VehicleId} descartada por {ErrorCode}.")]
    private static partial void PanicRejected(ILogger logger, string vehicleId, string errorCode);

    [LoggerMessage(EventId = 5004, Level = LogLevel.Warning,
        Message = "ALERTA DE PÁNICO registrada para {VehicleId}.")]
    private static partial void PanicRegistered(ILogger logger, string vehicleId);
}
