using FleetTelemetry.Application.Abstractions.Messaging;
using FleetTelemetry.Application.Alerts.GetRecentAlerts;
using FleetTelemetry.Application.Fleet.GetFleetSnapshot;
using FleetTelemetry.Application.Fleet.GetVehicleTrack;
using FleetTelemetry.Query.Api.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FleetTelemetry.Query.Api.Endpoints;

/// <summary>Lecturas que alimentan el dashboard.</summary>
internal static class FleetEndpoints
{
    public static IEndpointRouteBuilder MapFleetEndpoints(this IEndpointRouteBuilder builder)
    {
        var group = builder
            .MapGroup("/api/v1")
            .RequireAuthorization()
            .WithTags("Fleet");

        group.MapGet("/vehicles", async (IQueryDispatcher dispatcher, CancellationToken cancellationToken) =>
            {
                var result = await dispatcher
                    .QueryAsync(new GetFleetSnapshotQuery(), cancellationToken)
                    .ConfigureAwait(false);

                return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToProblem();
            })
            .WithName("GetFleetSnapshot")
            .WithSummary("Estado actual de todos los vehículos de la flota.");

        group.MapGet("/vehicles/{vehicleId}/track", async (
                string vehicleId,
                DateTimeOffset? from,
                DateTimeOffset? to,
                int? maxPoints,
                IQueryDispatcher dispatcher,
                CancellationToken cancellationToken) =>
            {
                // La ventana por defecto es la última hora: es lo que un operador mira al abrir el
                // recorrido de un vehículo, y evita que una petición sin parámetros barra el
                // histórico completo.
                var until = to ?? DateTimeOffset.UtcNow;
                var since = from ?? until.AddHours(-1);

                var result = await dispatcher
                    .QueryAsync(new GetVehicleTrackQuery(vehicleId, since, until, maxPoints ?? 500), cancellationToken)
                    .ConfigureAwait(false);

                return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToProblem();
            })
            .WithName("GetVehicleTrack")
            .WithSummary("Recorrido histórico de un vehículo.");

        group.MapGet("/alerts", async (
                int? limit,
                bool? onlyUnacknowledged,
                IQueryDispatcher dispatcher,
                CancellationToken cancellationToken) =>
            {
                var result = await dispatcher
                    .QueryAsync(
                        new GetRecentAlertsQuery(limit ?? 50, onlyUnacknowledged ?? false),
                        cancellationToken)
                    .ConfigureAwait(false);

                return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToProblem();
            })
            .WithName("GetRecentAlerts")
            .WithSummary("Alertas recientes de la flota.");

        return builder;
    }
}
