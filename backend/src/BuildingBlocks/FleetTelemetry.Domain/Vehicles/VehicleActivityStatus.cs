namespace FleetTelemetry.Domain.Vehicles;

/// <summary>
/// Estado operativo que ve el operador en el dashboard.
/// </summary>
/// <remarks>
/// Es un valor derivado, no almacenado: se calcula a partir de la última lectura y del último
/// movimiento. Guardarlo obligaría a mantenerlo sincronizado desde tres servicios y sería la primera
/// cosa en quedarse obsoleta.
/// </remarks>
public enum VehicleActivityStatus
{
    /// <summary>Sin lecturas recientes: probablemente sin cobertura o apagado.</summary>
    Offline = 0,
    Moving = 1,
    Stopped = 2,
    /// <summary>Detenido más allá del umbral, o con una alerta activa sin atender.</summary>
    Alerted = 3,
}
