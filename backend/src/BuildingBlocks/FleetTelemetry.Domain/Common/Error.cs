namespace FleetTelemetry.Domain.Common;

/// <summary>
/// Naturaleza del fallo, independiente del protocolo por el que se comunique.
/// </summary>
/// <remarks>
/// El dominio no conoce códigos HTTP, pero sí sabe distinguir "los datos son inválidos" de "eso no
/// existe". Traducir eso a 400 o 404 es responsabilidad del borde. Sin este tipo, el mapeo tendría
/// que inferirse del prefijo del código, y una convención de strings se rompe en silencio.
/// </remarks>
public enum ErrorType
{
    Validation = 0,
    NotFound = 1,
    Conflict = 2,
}

/// <summary>
/// Fallo esperado del sistema, identificado por un código estable.
/// </summary>
/// <remarks>
/// El código es parte del contrato: el frontend y los tests dependen de él, mientras que el mensaje
/// puede reescribirse o traducirse sin romper a nadie.
/// </remarks>
public sealed record Error(string Code, string Message, ErrorType Type)
{
    public static readonly Error None = new(string.Empty, string.Empty, ErrorType.Validation);

    public static Error Validation(string code, string message) => new(code, message, ErrorType.Validation);

    public static Error NotFound(string code, string message) => new(code, message, ErrorType.NotFound);

    public static Error Conflict(string code, string message) => new(code, message, ErrorType.Conflict);
}
