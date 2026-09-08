using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .WriteTo.Console());

builder.Services.AddOpenApi();
builder.Services.AddHealthChecks();

var app = builder.Build();

app.UseSerilogRequestLogging();
app.MapOpenApi();

// El health check es lo primero que existe: docker compose lo usa como condición de arranque
// del resto de servicios, así que tiene que responder desde el primer commit.
app.MapHealthChecks("/health");

await app.RunAsync().ConfigureAwait(false);
