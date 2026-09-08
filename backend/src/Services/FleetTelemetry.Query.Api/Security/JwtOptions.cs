namespace FleetTelemetry.Query.Api.Security;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string SigningKey { get; set; } = string.Empty;

    public string Issuer { get; set; } = "fleet-telemetry";

    public string Audience { get; set; } = "fleet-dashboard";

    public int LifetimeMinutes { get; set; } = 480;

    /// <summary>
    /// Credenciales del operador del dashboard.
    /// </summary>
    /// <remarks>
    /// Un usuario semilla en configuración, no una tabla de usuarios: el enunciado no pide gestión
    /// de identidades y construirla restaría tiempo a lo que sí evalúa. En producción esto sería
    /// Cognito o un IdP corporativo, y así está descrito en el README.
    /// </remarks>
    public string OperatorUsername { get; set; } = "operator";

    public string OperatorPassword { get; set; } = string.Empty;
}
