namespace FleetTelemetry.Infrastructure.Messaging;

/// <summary>Mensaje ya serializado, a la espera de que el broker vuelva.</summary>
/// <remarks>
/// Se guarda el JSON y no el objeto: el buffer no debe depender de que el tipo siga existiendo ni
/// obligar a mantener referencias vivas a objetos del dominio mientras dura la indisponibilidad.
/// </remarks>
internal sealed record PendingMessage(string RoutingKey, string Payload, string EventType, DateTimeOffset QueuedAt);
