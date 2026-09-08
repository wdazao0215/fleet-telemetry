using FleetTelemetry.Infrastructure;
using FleetTelemetry.Infrastructure.Messaging;
using FleetTelemetry.Ingestion.Api.Endpoints;
using FleetTelemetry.Ingestion.Api.Security;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .WriteTo.Console());

builder.Services.Configure<ApiKeyOptions>(builder.Configuration.GetSection(ApiKeyOptions.SectionName));

builder.Services
    .AddTelemetryCore(builder.Configuration)
    .AddRedisCache(builder.Configuration)
    .AddResilientEventBus(builder.Configuration)
    .AddIngestionUseCases();

builder.Services.AddScoped<ApiKeyEndpointFilter>();
builder.Services.AddOpenApi();

// Sin esto, una excepción no controlada devolvería HTML de error de ASP.NET a un cliente que
// espera JSON; el dispositivo a bordo no sabría interpretarlo.
builder.Services.AddProblemDetails();

builder.Services.AddHealthChecks();

var app = builder.Build();

app.UseSerilogRequestLogging();
app.UseExceptionHandler();
app.UseStatusCodePages();

app.MapOpenApi();
app.MapHealthChecks("/health");
app.MapTelemetryEndpoints();
app.MapDiagnosticsEndpoints();

await app.RunAsync().ConfigureAwait(false);

/// <summary>
/// Expuesto para que los tests de integración puedan arrancar el host real con WebApplicationFactory
/// en lugar de reconstruir la composición de servicios por su cuenta.
/// </summary>
public partial class Program;
