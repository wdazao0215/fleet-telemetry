using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace FleetTelemetry.Query.Api.Hubs;

/// <summary>
/// Canal en vivo hacia los dashboards conectados.
/// </summary>
/// <remarks>
/// El hub no expone métodos que el cliente pueda invocar: el flujo es unidireccional, del servidor
/// al navegador. Cualquier acción del operador —atender una alerta, borrar un vehículo— pasa por
/// HTTP, donde hay autorización, validación y trazas. Un hub con métodos de escritura sería una
/// segunda puerta de entrada con la mitad de las garantías.
/// </remarks>
[Authorize]
public sealed class TelemetryHub : Hub
{
    /// <summary>Nombres de los mensajes que recibe el cliente.</summary>
    public static class Events
    {
        public const string VehicleUpdated = "vehicleUpdated";
        public const string AlertRaised = "alertRaised";
    }
}
