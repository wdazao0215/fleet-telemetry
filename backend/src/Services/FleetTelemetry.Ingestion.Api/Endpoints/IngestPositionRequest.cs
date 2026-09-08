namespace FleetTelemetry.Ingestion.Api.Endpoints;

/// <summary>
/// Payload de telemetría tal y como lo envía el dispositivo.
/// </summary>
/// <remarks>
/// Los campos son anulables a propósito. Si <c>Latitude</c> fuese <c>double</c>, un payload sin ese
/// campo llegaría como 0,0 —una coordenada válida en el golfo de Guinea— y se aceptaría como buena.
/// Siendo anulable, la ausencia se distingue del cero y se rechaza con un error que nombra el campo.
/// </remarks>
public sealed record IngestPositionRequest(
    string? VehicleId,
    double? Latitude,
    double? Longitude,
    DateTimeOffset? Timestamp);
