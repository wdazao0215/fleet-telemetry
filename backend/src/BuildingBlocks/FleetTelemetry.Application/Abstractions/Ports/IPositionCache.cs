using FleetTelemetry.Application.Abstractions.Models;
using FleetTelemetry.Domain.Alerts;
using FleetTelemetry.Domain.Telemetry;
using FleetTelemetry.Domain.Vehicles;

namespace FleetTelemetry.Application.Abstractions.Ports;

/// <summary>
/// Estado caliente de la flota: deduplicación, última posición y control de alertas repetidas.
/// </summary>
/// <remarks>
/// Todas las operaciones llevan TTL. La caché es reconstruible a partir del histórico, así que
/// prefiere perder datos a acumularlos: sin expiración, un vehículo dado de baja dejaría claves
/// vivas para siempre.
/// </remarks>
public interface IPositionCache
{
    /// <summary>
    /// Registra la lectura como vista y responde si es la primera vez dentro de la ventana.
    /// </summary>
    /// <returns><c>true</c> si es nueva; <c>false</c> si es un duplicado que debe descartarse.</returns>
    /// <remarks>
    /// Comprobar y registrar son una sola operación a propósito: con dos llamadas separadas, dos
    /// peticiones simultáneas con la misma coordenada pasarían ambas la comprobación antes de que
    /// ninguna escribiera. La implementación usa SET NX, que es atómico.
    /// </remarks>
    Task<bool> TryRegisterUniqueReadingAsync(
        TelemetryReading reading,
        TimeSpan deduplicationWindow,
        CancellationToken cancellationToken);

    Task<MovementSnapshot?> GetLastMovementAsync(VehicleId vehicleId, CancellationToken cancellationToken);

    Task SaveLiveStateAsync(LivePosition livePosition, TimeSpan timeToLive, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<LivePosition>> GetLiveStateAsync(CancellationToken cancellationToken);

    Task<LivePosition?> GetLiveStateAsync(VehicleId vehicleId, CancellationToken cancellationToken);

    /// <summary>
    /// Marca que ya se alertó por este motivo y responde si corresponde emitir la alerta.
    /// </summary>
    /// <remarks>
    /// Un vehículo detenido emite posiciones cada 2-5 segundos y cada una vuelve a cumplir la
    /// condición de detención. Sin esta marca con TTL, un solo camión aparcado generaría cientos de
    /// alertas idénticas y haría inservible el panel.
    /// </remarks>
    Task<bool> TryMarkAlertRaisedAsync(
        VehicleId vehicleId,
        AlertKind kind,
        TimeSpan cooldown,
        CancellationToken cancellationToken);

    /// <summary>Elimina todo rastro del vehículo en caché. Paso compensable de la saga de borrado.</summary>
    Task PurgeVehicleAsync(VehicleId vehicleId, CancellationToken cancellationToken);
}
