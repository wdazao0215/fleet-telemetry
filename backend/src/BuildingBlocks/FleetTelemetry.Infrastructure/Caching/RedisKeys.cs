using System.Globalization;
using FleetTelemetry.Domain.Alerts;
using FleetTelemetry.Domain.Telemetry;
using FleetTelemetry.Domain.Vehicles;

namespace FleetTelemetry.Infrastructure.Caching;

/// <summary>
/// Espacio de claves de Redis, en un solo sitio.
/// </summary>
/// <remarks>
/// Construir claves ad hoc en cada llamada es cómo se acaba con dos formatos para lo mismo y un bug
/// que solo aparece en producción, cuando el escritor y el lector no coinciden.
/// </remarks>
internal static class RedisKeys
{
    private const string Prefix = "fleet";

    /// <summary>
    /// Clave de deduplicación de una lectura concreta.
    /// </summary>
    /// <remarks>
    /// Incluye el timestamp de la lectura, y esto es deliberado. Sin él, la clave sería
    /// (vehículo + coordenada) y un vehículo detenido —que emite la misma coordenada cada 2-5
    /// segundos— vería descartadas todas sus lecturas salvo la primera. El worker dejaría de
    /// recibir eventos suyos y la alerta de "Vehículo Detenido" no llegaría jamás: la deduplicación
    /// habría desactivado justo la funcionalidad que el enunciado pide.
    ///
    /// Con el timestamp dentro, se descarta lo que de verdad es un duplicado (el mismo paquete
    /// reenviado, que es lo que inyecta el simulador) y se deja pasar la emisión legítima de un
    /// vehículo parado.
    /// </remarks>
    public static string Deduplication(TelemetryReading reading) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"{Prefix}:dedupe:{reading.VehicleId.Value}:{reading.Position.ToCacheKey()}:{reading.RecordedAt.ToUnixTimeMilliseconds()}");

    /// <summary>Último estado conocido, lo que el dashboard lee para pintar el mapa.</summary>
    public static string LiveState(VehicleId vehicleId) => $"{Prefix}:vehicle:{vehicleId.Value}:live";

    /// <summary>Ancla desde la que se mide cuánto lleva parado el vehículo.</summary>
    public static string MovementAnchor(VehicleId vehicleId) => $"{Prefix}:vehicle:{vehicleId.Value}:movement";

    /// <summary>Marca de alerta emitida, para no repetirla en cada lectura.</summary>
    public static string AlertCooldown(VehicleId vehicleId, AlertKind kind) =>
        $"{Prefix}:alert:{kind}:{vehicleId.Value}";

    /// <summary>
    /// Índice de vehículos con estado en caché.
    /// </summary>
    /// <remarks>
    /// Existe para no usar KEYS ni SCAN al listar la flota: KEYS bloquea el servidor y SCAN obliga a
    /// paginar sobre todo el espacio de claves en cada refresco del dashboard.
    /// </remarks>
    public const string LiveIndex = $"{Prefix}:vehicles:live";

    public static string VehicleKeyPattern(VehicleId vehicleId) => $"{Prefix}:*:{vehicleId.Value}*";
}
