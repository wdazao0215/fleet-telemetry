using FleetTelemetry.Infrastructure;
using FleetTelemetry.Processing.Worker.Consumers;
using Serilog;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddSerilog((_, configuration) => configuration
    .ReadFrom.Configuration(builder.Configuration)
    .WriteTo.Console());

builder.Services
    .AddTelemetryCore(builder.Configuration)
    .AddFleetPersistence(builder.Configuration)
    .AddRedisCache(builder.Configuration)
    .AddResilientEventBus(builder.Configuration)
    .AddProcessingUseCases();

builder.Services.AddHostedService<PositionProcessingConsumer>();

var host = builder.Build();
await host.RunAsync().ConfigureAwait(false);
