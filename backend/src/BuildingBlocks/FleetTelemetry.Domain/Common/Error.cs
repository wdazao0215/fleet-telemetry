namespace FleetTelemetry.Domain.Common;

/// <summary>
/// Fallo esperado del sistema, identificado por un código estable.
/// </summary>
/// <remarks>
/// El código es parte del contrato: el frontend y los tests dependen de él, mientras que el mensaje
/// puede reescribirse o traducirse sin romper a nadie.
/// </remarks>
public sealed record Error(string Code, string Message)
{
    public static readonly Error None = new(string.Empty, string.Empty);

    public static Error Validation(string code, string message) => new(code, message);

    public static Error NotFound(string code, string message) => new(code, message);

    public static Error Conflict(string code, string message) => new(code, message);
}
