namespace FleetTelemetry.Infrastructure.Caching;

public sealed class RedisOptions
{
    public const string SectionName = "Redis";

    public string ConnectionString { get; set; } = "localhost:6379";

    /// <summary>
    /// Permite arrancar el servicio aunque Redis todavía no responda.
    /// </summary>
    /// <remarks>
    /// En docker compose los contenedores arrancan en paralelo. Sin esto, la API moriría en el
    /// primer intento de conexión en lugar de esperar a que Redis termine de levantarse.
    /// </remarks>
    public bool AbortOnConnectFail { get; set; }

    public int ConnectTimeoutMilliseconds { get; set; } = 5_000;
}
