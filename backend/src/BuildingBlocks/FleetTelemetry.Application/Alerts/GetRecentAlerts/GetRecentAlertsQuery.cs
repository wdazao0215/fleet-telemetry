using FleetTelemetry.Application.Abstractions.Messaging;

namespace FleetTelemetry.Application.Alerts.GetRecentAlerts;

public sealed record GetRecentAlertsQuery(int Limit = 50, bool OnlyUnacknowledged = false)
    : IQuery<IReadOnlyList<AlertDto>>;

public sealed record AlertDto(
    Guid Id,
    string VehicleId,
    string Kind,
    double Latitude,
    double Longitude,
    DateTimeOffset RaisedAt,
    DateTimeOffset? AcknowledgedAt,
    string Detail);
