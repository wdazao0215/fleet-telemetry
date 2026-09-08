using FleetTelemetry.Migrator;
using Npgsql;
using Serilog;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddSerilog((_, configuration) => configuration
    .ReadFrom.Configuration(builder.Configuration)
    .WriteTo.Console());

using var host = builder.Build();

var logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger<SchemaMigrator>();
var connectionString = builder.Configuration.GetConnectionString("FleetDb")
    ?? throw new InvalidOperationException("Falta la cadena de conexión 'ConnectionStrings:FleetDb'.");

var migrator = new SchemaMigrator(connectionString, logger);

// docker compose arranca los contenedores en paralelo y el healthcheck de Postgres puede pasar unos
// milisegundos antes de que acepte conexiones de verdad. Reintentar aquí evita que el primer
// 'docker compose up' de un clon nuevo falle por una carrera de arranque.
const int maxAttempts = 15;

for (var attempt = 1; attempt <= maxAttempts; attempt++)
{
    try
    {
        await migrator.MigrateAsync(CancellationToken.None).ConfigureAwait(false);
        Log.Information("Esquema al día.");
        return 0;
    }
    catch (NpgsqlException exception) when (attempt < maxAttempts)
    {
        Log.Warning(
            "La base de datos no está lista (intento {Attempt}/{MaxAttempts}): {Reason}",
            attempt,
            maxAttempts,
            exception.Message);

        await Task.Delay(TimeSpan.FromSeconds(2), CancellationToken.None).ConfigureAwait(false);
    }
}

Log.Fatal("No se pudo aplicar el esquema tras {MaxAttempts} intentos.", maxAttempts);
return 1;
