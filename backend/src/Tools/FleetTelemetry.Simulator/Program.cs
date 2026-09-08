using Serilog;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddSerilog((services, configuration) => configuration
    .ReadFrom.Configuration(builder.Configuration)
    .WriteTo.Console());

var host = builder.Build();
await host.RunAsync().ConfigureAwait(false);
