using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FleetTelemetry.Simulator;

/// <summary>
/// Genera el tráfico de telemetría con el que se evalúa el sistema.
/// </summary>
/// <remarks>
/// Además de las lecturas correctas, inyecta a propósito duplicados y payloads malformados en las
/// proporciones que fija el enunciado. Es tanto un generador de carga como una batería de pruebas
/// continua: si la API dejara de rechazar un payload inválido, se vería en el contador.
/// </remarks>
internal sealed partial class TelemetrySimulator(
    IHttpClientFactory httpClientFactory,
    IOptions<SimulatorOptions> options,
    ILogger<TelemetrySimulator> logger) : BackgroundService
{
    private readonly SimulatorOptions settings = options.Value;
    private readonly Random random = new(options.Value.RandomSeed);

    private int sent;
    private int duplicates;
    private int malformed;
    private int rejected;
    private int failures;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var vehicles = CreateFleet();
        var client = httpClientFactory.CreateClient("ingestion");

        SimulatorStarted(logger, vehicles.Count, settings.StationaryVehicleCount);

        // Un bucle por vehículo, cada uno con su propio ritmo: un bucle único que recorriera la
        // flota emitiría ráfagas sincronizadas, que no se parecen en nada al tráfico real de
        // dispositivos independientes.
        var loops = vehicles.Select(vehicle => RunVehicleLoopAsync(vehicle, client, stoppingToken));

        var reporting = ReportStatisticsAsync(stoppingToken);

        await Task.WhenAll([.. loops, reporting]).ConfigureAwait(false);
    }

    private List<SimulatedVehicle> CreateFleet()
    {
        var fleet = new List<SimulatedVehicle>(settings.VehicleCount);

        for (var index = 0; index < settings.VehicleCount; index++)
        {
            // Repartidos por Bogotá, que es donde apunta el mapa del dashboard.
            var latitude = 4.60 + (random.NextDouble() * 0.15);
            var longitude = -74.15 + (random.NextDouble() * 0.10);

            fleet.Add(new SimulatedVehicle(
                string.Create(CultureInfo.InvariantCulture, $"VH-{index + 1:D3}"),
                latitude,
                longitude,
                isStationary: index < settings.StationaryVehicleCount,
                new Random(settings.RandomSeed + index)));
        }

        return fleet;
    }

    private async Task RunVehicleLoopAsync(
        SimulatedVehicle vehicle,
        HttpClient client,
        CancellationToken stoppingToken)
    {
        var vehicleRandom = new Random(vehicle.Id.GetHashCode(StringComparison.Ordinal));

        while (!stoppingToken.IsCancellationRequested)
        {
            var interval = TimeSpan.FromMilliseconds(
                vehicleRandom.Next(settings.MinIntervalMilliseconds, settings.MaxIntervalMilliseconds));

            try
            {
                await Task.Delay(interval, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            vehicle.Advance(interval);

            var payload = BuildPayload(vehicle, vehicleRandom, out var isMalformed);

            await SendAsync(client, payload, isMalformed, stoppingToken).ConfigureAwait(false);

            // El duplicado se envía inmediatamente después del original, que es como ocurre en la
            // realidad: un reintento del dispositivo cuando no llegó el acuse.
            if (!isMalformed && vehicleRandom.NextDouble() < settings.DuplicateRate)
            {
                Interlocked.Increment(ref duplicates);
                await SendAsync(client, payload, isMalformed: false, stoppingToken).ConfigureAwait(false);
            }
        }
    }

    private TelemetryPayload BuildPayload(SimulatedVehicle vehicle, Random vehicleRandom, out bool isMalformed)
    {
        isMalformed = vehicleRandom.NextDouble() < settings.MalformedRate;

        if (!isMalformed)
        {
            return new TelemetryPayload(
                vehicle.Id, vehicle.Latitude, vehicle.Longitude, DateTimeOffset.UtcNow);
        }

        Interlocked.Increment(ref malformed);

        // Cuatro formas distintas de estar mal, para ejercitar el manejo de errores entero y no
        // solo la rama más fácil: fuera de rango, campo ausente, y timestamp en el futuro.
        return vehicleRandom.Next(4) switch
        {
            0 => new TelemetryPayload(vehicle.Id, 999.0, vehicle.Longitude, DateTimeOffset.UtcNow),
            1 => new TelemetryPayload(null, vehicle.Latitude, vehicle.Longitude, DateTimeOffset.UtcNow),
            2 => new TelemetryPayload(vehicle.Id, vehicle.Latitude, -400.0, DateTimeOffset.UtcNow),
            _ => new TelemetryPayload(vehicle.Id, vehicle.Latitude, vehicle.Longitude, DateTimeOffset.UtcNow.AddHours(2)),
        };
    }

    private async Task SendAsync(
        HttpClient client,
        TelemetryPayload payload,
        bool isMalformed,
        CancellationToken stoppingToken)
    {
        try
        {
            using var response = await client
                .PostAsJsonAsync(settings.IngestionUrl, payload, stoppingToken)
                .ConfigureAwait(false);

            Interlocked.Increment(ref sent);

            if (!response.IsSuccessStatusCode)
            {
                Interlocked.Increment(ref rejected);

                // Un rechazo de un payload que se envió mal a propósito es el comportamiento
                // correcto; solo se registra como anomalía el rechazo de uno que era válido.
                if (!isMalformed)
                {
                    UnexpectedRejection(logger, (int)response.StatusCode);
                }
            }
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            Interlocked.Increment(ref failures);
        }
    }

    private async Task ReportStatisticsAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));

        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
        {
            var totalSent = Volatile.Read(ref sent);
            var totalDuplicates = Volatile.Read(ref duplicates);
            var totalMalformed = Volatile.Read(ref malformed);
            var totalRejected = Volatile.Read(ref rejected);
            var totalFailures = Volatile.Read(ref failures);

            Statistics(logger, totalSent, totalDuplicates, totalMalformed, totalRejected, totalFailures);
        }
    }

    /// <summary>Las cuatro formas de payload inválido que inyecta el simulador.</summary>
    /// <remarks>
    /// Es un record y no un tipo anónimo porque las cuatro variantes difieren en la nulabilidad de
    /// vehicleId, y el compilador no puede unificarlas.
    /// </remarks>
    private sealed record TelemetryPayload(
        string? VehicleId, double? Latitude, double? Longitude, DateTimeOffset? Timestamp);

    [LoggerMessage(EventId = 6001, Level = LogLevel.Information,
        Message = "Simulador arrancado con {VehicleCount} vehículos ({StationaryCount} detenidos).")]
    private static partial void SimulatorStarted(ILogger logger, int vehicleCount, int stationaryCount);

    [LoggerMessage(EventId = 6002, Level = LogLevel.Information,
        Message = "Enviadas {Sent} | duplicadas {Duplicates} | malformadas {Malformed} | rechazadas {Rejected} | fallos de red {Failures}")]
    private static partial void Statistics(
        ILogger logger, int sent, int duplicates, int malformed, int rejected, int failures);

    [LoggerMessage(EventId = 6003, Level = LogLevel.Warning,
        Message = "La ingesta rechazó con {StatusCode} una lectura que era válida.")]
    private static partial void UnexpectedRejection(ILogger logger, int statusCode);
}
