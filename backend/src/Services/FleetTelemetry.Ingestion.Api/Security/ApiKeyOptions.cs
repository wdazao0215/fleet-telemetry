namespace FleetTelemetry.Ingestion.Api.Security;

public sealed class ApiKeyOptions
{
    public const string SectionName = "Ingestion";

    public const string HeaderName = "X-Api-Key";

    /// <summary>Clave que presenta el dispositivo a bordo del vehículo.</summary>
    /// <remarks>
    /// Una sola clave compartida es suficiente para un prototipo, pero no para producción: no
    /// permite revocar un dispositivo concreto. El README describe el esquema real —una credencial
    /// por dispositivo con rotación— en "Desafíos y Soluciones".
    /// </remarks>
    public string ApiKey { get; set; } = string.Empty;
}
