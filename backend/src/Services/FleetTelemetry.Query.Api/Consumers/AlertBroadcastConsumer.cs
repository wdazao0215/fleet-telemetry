using FleetTelemetry.Contracts;
using FleetTelemetry.Contracts.Events;
using FleetTelemetry.Infrastructure.Messaging;
using FleetTelemetry.Query.Api.Hubs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace FleetTelemetry.Query.Api.Consumers;

internal sealed class AlertBroadcastConsumer(
    RabbitMqConnectionProvider connectionProvider,
    IHubContext<TelemetryHub> hub,
    ILogger<AlertBroadcastConsumer> logger)
    : RabbitMqConsumerService<AlertRaisedEvent>(connectionProvider, logger)
{
    protected override string QueueName => Topology.Queues.AlertFanout;

    protected override bool UsesExclusiveQueue => true;

    protected override string BindingRoutingKey => Topology.RoutingKeys.AlertRaised;

    protected override async Task HandleAsync(AlertRaisedEvent message, CancellationToken cancellationToken) =>
        await hub.Clients.All
            .SendAsync(TelemetryHub.Events.AlertRaised, message, cancellationToken)
            .ConfigureAwait(false);
}
