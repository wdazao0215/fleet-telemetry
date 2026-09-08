using FleetTelemetry.Domain.Telemetry;

namespace FleetTelemetry.Domain.Vehicles;

/// <summary>
/// Deriva el estado operativo del vehículo a partir de sus últimas señales.
/// </summary>
public static class VehicleActivityEvaluator
{
    /// <summary>
    /// Ventana sin lecturas tras la cual se considera al vehículo desconectado.
    /// </summary>
    /// <remarks>
    /// El simulador emite cada 2-5 s. Treinta segundos son varias emisiones perdidas seguidas:
    /// suficiente para distinguir una desconexión real de un paquete que se retrasó.
    /// </remarks>
    public static readonly TimeSpan OfflineThreshold = TimeSpan.FromSeconds(30);

    public static VehicleActivityStatus Resolve(
        DateTimeOffset? lastSeenAt,
        MovementSnapshot? lastMovement,
        bool hasUnacknowledgedAlert,
        DateTimeOffset now,
        StoppedVehiclePolicy stoppedPolicy)
    {
        ArgumentNullException.ThrowIfNull(stoppedPolicy);

        // La alerta manda sobre todo lo demás: un vehículo con alerta sin atender no debe
        // esconderse en el dashboard porque además esté desconectado.
        if (hasUnacknowledgedAlert)
        {
            return VehicleActivityStatus.Alerted;
        }

        if (lastSeenAt is null || now - lastSeenAt > OfflineThreshold)
        {
            return VehicleActivityStatus.Offline;
        }

        if (lastMovement is null)
        {
            return VehicleActivityStatus.Moving;
        }

        var stationaryFor = lastSeenAt.Value - lastMovement.ObservedAt;
        return stationaryFor >= stoppedPolicy.Threshold
            ? VehicleActivityStatus.Alerted
            : stationaryFor > TimeSpan.Zero
                ? VehicleActivityStatus.Stopped
                : VehicleActivityStatus.Moving;
    }
}
