using FleetTelemetry.Application.Abstractions.Messaging;

namespace FleetTelemetry.Application.Fleet.CompleteVehicleDeletion;

public sealed record CompleteVehicleDeletionCommand(string VehicleId, string CorrelationId)
    : ICommand<CompleteVehicleDeletionResult>;

public sealed record CompleteVehicleDeletionResult(string VehicleId, bool Succeeded, string? FailureReason);
