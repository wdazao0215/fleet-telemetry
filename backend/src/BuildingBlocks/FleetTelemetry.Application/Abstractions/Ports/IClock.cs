namespace FleetTelemetry.Application.Abstractions.Ports;

/// <summary>
/// Fuente de tiempo del sistema.
/// </summary>
/// <remarks>
/// La regla central del enunciado es "misma coordenada durante más de un minuto". Con
/// <c>DateTimeOffset.UtcNow</c> incrustado en los handlers, comprobar esa regla exigiría tests que
/// esperan sesenta segundos reales. Con este puerto se comprueba en microsegundos y sobre el borde
/// exacto.
/// </remarks>
public interface IClock
{
    DateTimeOffset UtcNow { get; }
}
