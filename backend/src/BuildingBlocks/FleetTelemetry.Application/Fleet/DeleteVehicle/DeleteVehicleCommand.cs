using FleetTelemetry.Application.Abstractions.Messaging;

namespace FleetTelemetry.Application.Fleet.DeleteVehicle;

/// <summary>Inicia la eliminación de un vehículo del sistema.</summary>
public sealed record DeleteVehicleCommand(string VehicleId, string CorrelationId)
    : ICommand<DeleteVehicleResult>;

/// <param name="Accepted">
/// El borrado se aceptó y está en curso. La consistencia es eventual: el vehículo desaparecerá de
/// caché e histórico en cuanto el worker procese el evento.
/// </param>
public sealed record DeleteVehicleResult(string VehicleId, bool Accepted, string State);
