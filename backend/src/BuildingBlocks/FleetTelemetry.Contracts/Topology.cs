namespace FleetTelemetry.Contracts;

/// <summary>
/// Nombres de exchanges, colas y routing keys compartidos por productores y consumidores.
/// </summary>
/// <remarks>
/// Centralizados aquí porque un typo en una routing key no falla al compilar ni al arrancar: los
/// mensajes simplemente se publican a un vacío y el fallo aparece como "el worker no procesa nada".
/// </remarks>
public static class Topology
{
    public const string TelemetryExchange = "fleet.telemetry";
    public const string DeadLetterExchange = "fleet.telemetry.dlx";

    public static class RoutingKeys
    {
        public const string PositionAccepted = "position.accepted";
        public const string AlertRaised = "alert.raised";
        public const string VehicleDeletionRequested = "vehicle.deletion.requested";
        public const string VehicleDeletionCompleted = "vehicle.deletion.completed";
        public const string VehicleStateUpdated = "vehicle.state.updated";
        public const string PanicButtonPressed = "vehicle.panic";
    }

    public static class Queues
    {
        public const string PositionProcessing = "fleet.position-processing";
        public const string VehicleDeletion = "fleet.vehicle-deletion";
        public const string AlertFanout = "fleet.alert-fanout";
        public const string PanicProcessing = "fleet.panic-processing";
        public const string DeadLetter = "fleet.dead-letter";

        /// <summary>
        /// Prefijo de las colas efímeras que usan los servidores de tiempo real.
        /// </summary>
        /// <remarks>
        /// Cada instancia de Query.Api declara su propia cola exclusiva en lugar de compartir una
        /// duradera. Con una cola compartida, RabbitMQ repartiría los mensajes entre réplicas y solo
        /// los navegadores conectados a una de ellas verían cada actualización. El broadcast exige
        /// que todas las instancias reciban todo.
        /// </remarks>
        public const string LiveUpdatesPrefix = "fleet.live-updates";
    }
}
