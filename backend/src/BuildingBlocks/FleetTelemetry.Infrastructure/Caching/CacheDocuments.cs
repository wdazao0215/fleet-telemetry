namespace FleetTelemetry.Infrastructure.Caching;

/// <summary>
/// Representación serializable del estado en caché.
/// </summary>
/// <remarks>
/// Son tipos propios de infraestructura y no los del dominio: los value objects protegen sus
/// invariantes con constructores privados, así que un deserializador no puede reconstruirlos, y
/// abrirlos para complacer a System.Text.Json convertiría el modelo en un saco de propiedades.
/// La conversión se hace explícitamente al leer, revalidando.
/// </remarks>
internal sealed record LiveStateDocument(
    string VehicleId,
    double Latitude,
    double Longitude,
    DateTimeOffset RecordedAt,
    double? MovementLatitude,
    double? MovementLongitude,
    DateTimeOffset? MovementObservedAt);

internal sealed record MovementDocument(double Latitude, double Longitude, DateTimeOffset ObservedAt);
