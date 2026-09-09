using FleetTelemetry.Application.Abstractions.Messaging;
using FleetTelemetry.Application.Fleet.CompleteVehicleDeletion;
using FleetTelemetry.Contracts;
using FleetTelemetry.Contracts.Events;
using FleetTelemetry.Infrastructure.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FleetTelemetry.Processing.Worker.Consumers;

/// <summary>Ejecuta el segundo paso de la saga de eliminación de vehículos.</summary>
internal sealed partial class VehicleDeletionConsumer(
    RabbitMqConnectionProvider connectionProvider,
    IServiceScopeFactory scopeFactory,
    ILogger<VehicleDeletionConsumer> logger)
    : RabbitMqConsumerService<VehicleDeletionRequestedEvent>(connectionProvider, logger)
{
    protected override string QueueName => Topology.Queues.VehicleDeletion;

    /// <summary>
    /// Prefetch de 1: los borrados se procesan de uno en uno.
    /// </summary>
    /// <remarks>
    /// Cada borrado elimina potencialmente millones de filas del histórico. Procesar varios en
    /// paralelo competiría por la misma base de datos justo mientras la ingesta sigue escribiendo.
    /// </remarks>
    protected override ushort PrefetchCount => 1;

    protected override async Task HandleAsync(
        VehicleDeletionRequestedEvent message,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);

        await using var scope = scopeFactory.CreateAsyncScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<ICommandDispatcher>();

        var result = await dispatcher.SendAsync(
            new CompleteVehicleDeletionCommand(message.VehicleId, message.CorrelationId),
            cancellationToken).ConfigureAwait(false);

        if (result.IsFailure)
        {
            DeletionRejected(logger, message.VehicleId, result.Error.Code);
            return;
        }

        if (result.Value.Succeeded)
        {
            DeletionCompleted(logger, message.VehicleId);
        }
        else
        {
            DeletionCompensated(logger, message.VehicleId, result.Value.FailureReason ?? "desconocido");
        }
    }

    [LoggerMessage(EventId = 5005, Level = LogLevel.Information,
        Message = "Saga de eliminación completada para {VehicleId}: caché e histórico purgados.")]
    private static partial void DeletionCompleted(ILogger logger, string vehicleId);

    [LoggerMessage(EventId = 5006, Level = LogLevel.Error,
        Message = "Saga de eliminación compensada para {VehicleId}: {Reason}. Queda en DeletionFailed.")]
    private static partial void DeletionCompensated(ILogger logger, string vehicleId, string reason);

    [LoggerMessage(EventId = 5007, Level = LogLevel.Warning,
        Message = "Eliminación de {VehicleId} descartada por {ErrorCode}.")]
    private static partial void DeletionRejected(ILogger logger, string vehicleId, string errorCode);
}
