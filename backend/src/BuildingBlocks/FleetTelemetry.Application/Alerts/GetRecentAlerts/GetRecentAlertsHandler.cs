using FleetTelemetry.Application.Abstractions.Messaging;
using FleetTelemetry.Application.Abstractions.Ports;
using FleetTelemetry.Domain.Common;

namespace FleetTelemetry.Application.Alerts.GetRecentAlerts;

public sealed class GetRecentAlertsHandler(IAlertRepository alerts)
    : IQueryHandler<GetRecentAlertsQuery, IReadOnlyList<AlertDto>>
{
    public async Task<Result<IReadOnlyList<AlertDto>>> HandleAsync(
        GetRecentAlertsQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var limit = Math.Clamp(query.Limit, 1, 200);

        var recent = await alerts
            .ListRecentAsync(limit, query.OnlyUnacknowledged, cancellationToken)
            .ConfigureAwait(false);

        return Result.Success<IReadOnlyList<AlertDto>>(
        [
            .. recent.Select(alert => new AlertDto(
                alert.Id,
                alert.VehicleId.Value,
                alert.Kind.ToString(),
                alert.Position.Latitude,
                alert.Position.Longitude,
                alert.RaisedAt,
                alert.AcknowledgedAt,
                alert.Detail)),
        ]);
    }
}
