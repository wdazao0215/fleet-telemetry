namespace FleetTelemetry.Domain.Vehicles;

/// <summary>
/// Estado del vehículo dentro de la saga de eliminación.
/// </summary>
/// <remarks>
/// El borrado cruza dos almacenes (caché y base de datos) sin transacción común, así que necesita
/// estados intermedios explícitos. <see cref="DeletionFailed"/> existe para que un borrado a medias
/// sea visible y reintentable, en vez de un vehículo fantasma que quedó solo en Redis.
/// </remarks>
public enum VehicleLifecycleState
{
    Active = 0,
    PendingDeletion = 1,
    DeletionFailed = 2,
    Deleted = 3,
}
