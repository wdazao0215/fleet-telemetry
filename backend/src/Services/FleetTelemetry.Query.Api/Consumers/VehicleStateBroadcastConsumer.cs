using FleetTelemetry.Contracts;
using FleetTelemetry.Contracts.Events;
using FleetTelemetry.Infrastructure.Messaging;
using FleetTelemetry.Query.Api.Hubs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace FleetTelemetry.Query.Api.Consumers;

/// <summary>Reenvía por SignalR cada cambio de estado que publica el worker.</summary>
internal sealed class VehicleStateBroadcastConsumer(
    RabbitMqConnectionProvider connectionProvider,
    IHubContext<TelemetryHub> hub,
    ILogger<VehicleStateBroadcastConsumer> logger)
    : RabbitMqConsumerService<VehicleStateUpdatedEvent>(connectionProvider, logger)
{
    protected override string QueueName => Topology.Queues.LiveUpdatesPrefix;

    protected override bool UsesExclusiveQueue => true;

    protected override string BindingRoutingKey => Topology.RoutingKeys.VehicleStateUpdated;

    protected override async Task HandleAsync(
        VehicleStateUpdatedEvent message,
        CancellationToken cancellationToken) =>
        await hub.Clients.All
            .SendAsync(TelemetryHub.Events.VehicleUpdated, message, cancellationToken)
            .ConfigureAwait(false);
}
