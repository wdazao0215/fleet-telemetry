using FleetTelemetry.Application.Abstractions.Messaging;
using FleetTelemetry.Application.Alerts.RaisePanic;
using FleetTelemetry.Application.Telemetry.IngestPosition;
using FleetTelemetry.Domain.Common;
using FleetTelemetry.Ingestion.Api.Http;
using FleetTelemetry.Ingestion.Api.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace FleetTelemetry.Ingestion.Api.Endpoints;

/// <summary>
/// Superficie HTTP de la ingesta.
/// </summary>
/// <remarks>
/// Los endpoints traducen entre HTTP y el caso de uso, y nada más: ni una regla de negocio vive
/// aquí. Si esta clase creciera con condicionales, sería señal de que algo pertenece al handler.
/// </remarks>
internal static class TelemetryEndpoints
{
    private static readonly Error MissingFields = Error.Validation(
        "telemetry.missing_fields",
        "vehicleId, latitude, longitude y timestamp son obligatorios.");

    public static IEndpointRouteBuilder MapTelemetryEndpoints(this IEndpointRouteBuilder builder)
    {
        var group = builder
            .MapGroup("/api/v1")
            .AddEndpointFilter<ApiKeyEndpointFilter>()
            .WithTags("Telemetry");

        group.MapPost("/telemetry", IngestAsync)
            .WithName("IngestPosition")
            .WithSummary("Recibe una lectura GPS de un vehículo.")
            .Produces<IngestPositionResult>(StatusCodes.Status202Accepted)
            .Produces<IngestPositionResult>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        group.MapPost("/panic", RaisePanicAsync)
            .WithName("RaisePanic")
            .WithSummary("Registra la pulsación del botón de pánico del conductor.")
            .Produces<RaisePanicResult>(StatusCodes.Status202Accepted)
            .ProducesProblem(StatusCodes.Status400BadRequest);

        return builder;
    }

    private static async Task<IResult> RaisePanicAsync(
        PanicRequest request,
        ICommandDispatcher dispatcher,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (request.Latitude is not { } latitude || request.Longitude is not { } longitude)
        {
            return MissingFields.ToProblem();
        }

        var result = await dispatcher.SendAsync(
            new RaisePanicCommand(
                request.VehicleId,
                latitude,
                longitude,
                request.PressedAt ?? DateTimeOffset.UtcNow,
                CorrelationIdOf(httpContext)),
            cancellationToken).ConfigureAwait(false);

        return result.IsSuccess ? Results.Accepted(value: result.Value) : result.Error.ToProblem();
    }

    private static async Task<IResult> IngestAsync(
        IngestPositionRequest request,
        ICommandDispatcher dispatcher,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (request.Latitude is not { } latitude ||
            request.Longitude is not { } longitude ||
            request.Timestamp is not { } timestamp)
        {
            return MissingFields.ToProblem();
        }

        var command = new IngestPositionCommand(
            request.VehicleId,
            latitude,
            longitude,
            timestamp,
            CorrelationIdOf(httpContext));

        var result = await dispatcher.SendAsync(command, cancellationToken).ConfigureAwait(false);

        if (result.IsFailure)
        {
            return result.Error.ToProblem();
        }

        // 200 para el duplicado y 202 para la lectura nueva: el cliente distingue ambos casos sin
        // leer el cuerpo, y ninguno es un error. Un 409 haría que el simulador —que envía un 10% de
        // duplicados por diseño— pareciera estar fallando.
        return result.Value.Duplicate
            ? Results.Ok(result.Value)
            : Results.Accepted(value: result.Value);
    }

    /// <summary>
    /// Identificador para seguir la lectura por los tres servicios.
    /// </summary>
    /// <remarks>
    /// Se respeta el que traiga el cliente si lo envía: así una posición emitida por el móvil puede
    /// rastrearse desde el dispositivo hasta el dashboard con un solo identificador.
    /// </remarks>
    private static string CorrelationIdOf(HttpContext httpContext) =>
        httpContext.Request.Headers.TryGetValue("X-Correlation-Id", out var header) &&
        !string.IsNullOrWhiteSpace(header)
            ? header.ToString()
            : httpContext.TraceIdentifier;
}
