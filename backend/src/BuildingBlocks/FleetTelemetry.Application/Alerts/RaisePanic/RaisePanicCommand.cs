using FleetTelemetry.Application.Abstractions.Messaging;

namespace FleetTelemetry.Application.Alerts.RaisePanic;

/// <summary>El conductor pulsó el botón de pánico desde la aplicación móvil.</summary>
public sealed record RaisePanicCommand(
    string? VehicleId,
    double Latitude,
    double Longitude,
    DateTimeOffset PressedAt,
    string CorrelationId) : ICommand<RaisePanicResult>;

public sealed record RaisePanicResult(string VehicleId, DateTimeOffset AcceptedAt);
