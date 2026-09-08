using System.Security.Cryptography;
using System.Text;
using FleetTelemetry.Ingestion.Api.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace FleetTelemetry.Ingestion.Api.Security;

/// <summary>
/// Exige la API Key del dispositivo en los endpoints de ingesta.
/// </summary>
/// <remarks>
/// Se implementa como filtro de endpoint y no como middleware para que solo afecte a las rutas que
/// lo declaran: <c>/health</c> debe seguir respondiendo sin credenciales o el healthcheck de docker
/// compose marcaría el contenedor como caído.
/// </remarks>
internal sealed class ApiKeyEndpointFilter(IOptions<ApiKeyOptions> options) : IEndpointFilter
{
    private readonly byte[] expected = Encoding.UTF8.GetBytes(options.Value.ApiKey);

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);

        if (!context.HttpContext.Request.Headers.TryGetValue(ApiKeyOptions.HeaderName, out var provided))
        {
            return Unauthorized("Falta la cabecera de API Key.");
        }

        // Comparación en tiempo constante: comparar con == permitiría deducir la clave midiendo
        // cuánto tarda el rechazo, carácter a carácter.
        var candidate = Encoding.UTF8.GetBytes(provided.ToString());
        if (!CryptographicOperations.FixedTimeEquals(candidate, expected))
        {
            return Unauthorized("La API Key no es válida.");
        }

        return await next(context).ConfigureAwait(false);
    }

    private static IResult Unauthorized(string detail) => Results.Problem(new ProblemDetails
    {
        Status = StatusCodes.Status401Unauthorized,
        Title = "No autorizado",
        Detail = detail,
        Extensions = { ["code"] = "auth.invalid_api_key" },
    });
}
