namespace FleetTelemetry.Ingestion.Api.Endpoints;

/// <summary>
/// Pulsación del botón de pánico.
/// </summary>
/// <remarks>
/// <c>PressedAt</c> es opcional porque el botón puede pulsarse sin cobertura y sincronizarse
/// después: si el cliente conoce el momento real, manda ese; si no, se usa el de recepción.
/// </remarks>
public sealed record PanicRequest(
    string? VehicleId,
    double? Latitude,
    double? Longitude,
    DateTimeOffset? PressedAt);
