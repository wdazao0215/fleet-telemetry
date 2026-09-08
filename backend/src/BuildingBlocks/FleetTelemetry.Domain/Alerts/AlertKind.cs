namespace FleetTelemetry.Domain.Alerts;

public enum AlertKind
{
    /// <summary>El vehículo lleva más del umbral sin salir del radio de detención.</summary>
    StoppedVehicle = 0,

    /// <summary>El conductor pulsó el botón de pánico desde la aplicación móvil.</summary>
    PanicButton = 1,
}
