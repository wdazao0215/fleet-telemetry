using FleetTelemetry.Infrastructure.Messaging;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FleetTelemetry.Ingestion.Api.Endpoints;

/// <summary>
/// Diagnóstico de la resiliencia del servicio.
/// </summary>
/// <remarks>
/// Existe para hacer demostrable el Circuit Breaker: se apaga RabbitMQ, se sigue enviando telemetría
/// y este endpoint muestra el circuito abierto y el buffer creciendo. Sin él, la tolerancia a fallos
/// solo se podría afirmar, no enseñar.
/// </remarks>
internal static class DiagnosticsEndpoints
{
    public static IEndpointRouteBuilder MapDiagnosticsEndpoints(this IEndpointRouteBuilder builder)
    {
        builder.MapGet("/health/resilience", (ResilienceState state) => Results.Ok(new
        {
            circuitState = state.CircuitState,
            publisherHealthy = state.IsHealthy,
            bufferedMessages = state.BufferedMessages,
            droppedMessages = state.DroppedMessages,
        }))
        .WithName("ResilienceState")
        .WithSummary("Estado del circuit breaker de publicación y del buffer local.")
        .WithTags("Diagnostics");

        return builder;
    }
}
