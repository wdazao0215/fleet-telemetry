using FleetTelemetry.Query.Api.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace FleetTelemetry.Query.Api.Endpoints;

internal static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder builder)
    {
        builder.MapPost("/api/v1/auth/token", (LoginRequest request, TokenIssuer issuer) =>
        {
            if (!issuer.AreCredentialsValid(request.Username, request.Password))
            {
                // Un único mensaje para usuario inexistente y contraseña incorrecta: distinguirlos
                // permitiría enumerar usuarios válidos.
                return Results.Problem(new ProblemDetails
                {
                    Status = StatusCodes.Status401Unauthorized,
                    Title = "Credenciales inválidas",
                    Detail = "El usuario o la contraseña no son correctos.",
                    Extensions = { ["code"] = "auth.invalid_credentials" },
                });
            }

            var (token, expiresAt) = issuer.Issue(request.Username!);
            return Results.Ok(new LoginResponse(token, expiresAt));
        })
        .WithName("IssueToken")
        .WithSummary("Emite un token para el dashboard.")
        .WithTags("Auth")
        .AllowAnonymous();

        return builder;
    }
}

public sealed record LoginRequest(string? Username, string? Password);

public sealed record LoginResponse(string AccessToken, DateTimeOffset ExpiresAt);
