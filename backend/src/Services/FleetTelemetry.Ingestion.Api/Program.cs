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

// La PWA del conductor corre en otro origen y envía la API Key en una cabecera propia, lo que
// obliga al navegador a hacer preflight. Sin CORS, cada lectura se queda encolada en el dispositivo
// para siempre: el cliente parece estar sin cobertura cuando en realidad el servidor está al lado.
//
// Los orígenes se enumeran explícitamente en lugar de usar '*': este endpoint acepta credenciales
// de dispositivo, y abrirlo a cualquier origen permitiría que una página cualquiera enviase
// telemetría falsa si consiguiera una clave.
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? ["http://localhost:3000"];

builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy
    .WithOrigins(allowedOrigins)
    .WithHeaders("Content-Type", ApiKeyOptions.HeaderName, "X-Correlation-Id")
    .WithMethods("POST")));

var app = builder.Build();

app.UseSerilogRequestLogging();
app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseCors();

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
