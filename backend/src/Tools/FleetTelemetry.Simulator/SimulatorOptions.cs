namespace FleetTelemetry.Simulator;

/// <summary>
/// Parámetros del generador de tráfico.
/// </summary>
/// <remarks>
/// Los valores por defecto son los que exige el enunciado: al menos 5 vehículos emitiendo cada 2-5
/// segundos, con un 10% de peticiones duplicadas y un 5% con formato erróneo.
/// </remarks>
public sealed class SimulatorOptions
{
    public const string SectionName = "Simulator";

    public string IngestionUrl { get; set; } = "http://localhost:8081/api/v1/telemetry";

    public string ApiKey { get; set; } = "dev-fleet-key";

    public int VehicleCount { get; set; } = 6;

    public int MinIntervalMilliseconds { get; set; } = 2_000;

    public int MaxIntervalMilliseconds { get; set; } = 5_000;

    public double DuplicateRate { get; set; } = 0.10;

    public double MalformedRate { get; set; } = 0.05;

    /// <summary>
    /// Vehículos que se quedan quietos para provocar la alerta de detención.
    /// </summary>
    /// <remarks>
    /// Sin al menos uno, la alerta más importante del sistema no se podría ver funcionando en una
    /// demo sin esperar a que algo se detenga por casualidad.
    /// </remarks>
    public int StationaryVehicleCount { get; set; } = 1;

    /// <summary>Semilla del generador aleatorio; fija el escenario para poder reproducir un fallo.</summary>
    public int RandomSeed { get; set; } = 20260908;
}
