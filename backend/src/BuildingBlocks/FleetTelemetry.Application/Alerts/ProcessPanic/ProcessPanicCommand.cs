using FleetTelemetry.Application.Abstractions.Messaging;

namespace FleetTelemetry.Application.Alerts.ProcessPanic;

public sealed record ProcessPanicCommand(
    string VehicleId,
    double Latitude,
    double Longitude,
    DateTimeOffset PressedAt,
    string CorrelationId) : ICommand<ProcessPanicResult>;

public sealed record ProcessPanicResult(Guid AlertId);
