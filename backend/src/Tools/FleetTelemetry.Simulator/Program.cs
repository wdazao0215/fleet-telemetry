using FleetTelemetry.Simulator;
using Serilog;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddSerilog((_, configuration) => configuration
    .ReadFrom.Configuration(builder.Configuration)
    .WriteTo.Console());

builder.Services.Configure<SimulatorOptions>(builder.Configuration.GetSection(SimulatorOptions.SectionName));

var settings = builder.Configuration.GetSection(SimulatorOptions.SectionName).Get<SimulatorOptions>()
    ?? new SimulatorOptions();

builder.Services.AddHttpClient("ingestion", client =>
{
    client.DefaultRequestHeaders.Add("X-Api-Key", settings.ApiKey);

    // Timeout corto a propósito: si la ingesta tarda más de 5 s, algo va mal y el simulador debe
    // seguir generando carga en lugar de quedarse bloqueado esperando.
    client.Timeout = TimeSpan.FromSeconds(5);
});

builder.Services.AddHostedService<TelemetrySimulator>();

var host = builder.Build();
await host.RunAsync().ConfigureAwait(false);
