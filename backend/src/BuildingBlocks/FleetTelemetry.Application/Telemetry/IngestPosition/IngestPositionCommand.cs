using FleetTelemetry.Application.Abstractions.Messaging;

namespace FleetTelemetry.Application.Telemetry.IngestPosition;

/// <summary>
/// Registrar una lectura GPS recibida de un vehículo.
/// </summary>
/// <remarks>
/// Los tipos son primitivos porque este comando se construye directamente desde el borde HTTP, donde
/// todavía nada está validado. Convertirlos a value objects del dominio es responsabilidad del
/// handler, y ahí es donde un payload malformado se rechaza.
/// </remarks>
public sealed record IngestPositionCommand(
    string? VehicleId,
    double Latitude,
    double Longitude,
    DateTimeOffset RecordedAt,
    string CorrelationId) : ICommand<IngestPositionResult>;

/// <param name="Duplicate">
/// La lectura ya se había recibido dentro de la ventana y no se propagó. No es un error: el
/// enunciado inyecta un 10% de duplicados a propósito.
/// </param>
public sealed record IngestPositionResult(string VehicleId, bool Duplicate, DateTimeOffset AcceptedAt);
