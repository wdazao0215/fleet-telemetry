using FleetTelemetry.Domain.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace FleetTelemetry.Query.Api.Http;

/// <summary>
/// Traduce un <see cref="Error"/> del dominio a una respuesta HTTP.
/// </summary>
/// <remarks>
/// Es el único punto del servicio que conoce códigos de estado. El enunciado inyecta un 5% de
/// payloads malformados a propósito, así que la respuesta de error es tan parte del producto como la
/// del camino feliz: ProblemDetails (RFC 7807) da al cliente un formato predecible en vez de un
/// mensaje suelto.
/// </remarks>
public static class ErrorResults
{
    public static IResult ToProblem(this Error error)
    {
        ArgumentNullException.ThrowIfNull(error);

        var statusCode = error.Type switch
        {
            ErrorType.NotFound => StatusCodes.Status404NotFound,
            ErrorType.Conflict => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status400BadRequest,
        };

        return Results.Problem(new ProblemDetails
        {
            Status = statusCode,
            Title = TitleFor(error.Type),
            Detail = error.Message,
            // El código viaja como extensión para que el cliente pueda reaccionar de forma
            // programática sin parsear el texto, que puede cambiar o traducirse.
            Extensions = { ["code"] = error.Code },
        });
    }

    private static string TitleFor(ErrorType type) => type switch
    {
        ErrorType.NotFound => "Recurso no encontrado",
        ErrorType.Conflict => "Conflicto con el estado actual",
        _ => "Datos de telemetría inválidos",
    };
}
