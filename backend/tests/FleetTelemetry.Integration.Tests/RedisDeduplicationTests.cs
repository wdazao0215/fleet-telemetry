using FleetTelemetry.Domain.Telemetry;
using FleetTelemetry.Infrastructure.Caching;
using Shouldly;

namespace FleetTelemetry.Integration.Tests;

/// <summary>
/// La deduplicación, contra un Redis de verdad.
/// </summary>
/// <remarks>
/// Es el test que cierra el riesgo más caro del sistema: si esta lógica se rompe, la alerta de
/// "Vehículo Detenido" deja de dispararse **en silencio**, sin ningún error visible.
/// </remarks>
[Collection(nameof(SharedRedis))]
public class RedisDeduplicationTests(RedisFixture redis)
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan Window = TimeSpan.FromSeconds(10);

    [Fact]
    public async Task TryRegisterUniqueReading_TheSamePacketTwice_IsRejectedTheSecondTime()
    {
        var cache = new RedisPositionCache(redis.Connection);
        var reading = Reading("VH-DEDUPE-1", 4.7110, -74.0721, Now);

        var first = await cache.TryRegisterUniqueReadingAsync(reading, Window, CancellationToken.None);
        var second = await cache.TryRegisterUniqueReadingAsync(reading, Window, CancellationToken.None);

        first.ShouldBeTrue();
        second.ShouldBeFalse();
    }

    [Fact]
    public async Task TryRegisterUniqueReading_SameCoordinateWithADifferentTimestamp_IsAccepted()
    {
        // EL test más importante del repositorio. Un vehículo detenido emite la misma coordenada
        // cada 2-5 segundos; si estas lecturas se trataran como duplicadas, el worker dejaría de
        // recibir sus eventos y la alerta de vehículo detenido no se generaría nunca.
        var cache = new RedisPositionCache(redis.Connection);

        var atTenOClock = Reading("VH-PARKED-1", 4.6500, -74.1000, Now);
        var threeSecondsLater = Reading("VH-PARKED-1", 4.6500, -74.1000, Now.AddSeconds(3));

        var first = await cache.TryRegisterUniqueReadingAsync(atTenOClock, Window, CancellationToken.None);
        var second = await cache.TryRegisterUniqueReadingAsync(threeSecondsLater, Window, CancellationToken.None);

        first.ShouldBeTrue();
        second.ShouldBeTrue("un vehículo parado sigue emitiendo, y esas lecturas deben propagarse");
    }

    [Fact]
    public async Task TryRegisterUniqueReading_ForDifferentVehicles_DoesNotCollide()
    {
        // Comparten coordenada y timestamp: dos vehículos parados juntos en un depósito.
        var cache = new RedisPositionCache(redis.Connection);

        var first = await cache.TryRegisterUniqueReadingAsync(
            Reading("VH-A", 4.7110, -74.0721, Now), Window, CancellationToken.None);
        var second = await cache.TryRegisterUniqueReadingAsync(
            Reading("VH-B", 4.7110, -74.0721, Now), Window, CancellationToken.None);

        first.ShouldBeTrue();
        second.ShouldBeTrue();
    }

    [Fact]
    public async Task TryRegisterUniqueReading_UnderConcurrency_AcceptsExactlyOne()
    {
        // El motivo de usar SET NX en lugar de comprobar y luego escribir. Con dos operaciones
        // separadas, todas estas llamadas leerían "no existe" antes de que ninguna escribiera.
        var cache = new RedisPositionCache(redis.Connection);
        var reading = Reading("VH-RACE-1", 4.7110, -74.0721, Now);

        var attempts = await Task.WhenAll(
            Enumerable.Range(0, 25).Select(_ =>
                cache.TryRegisterUniqueReadingAsync(reading, Window, CancellationToken.None)));

        attempts.Count(accepted => accepted).ShouldBe(1);
    }

    [Fact]
    public async Task TryRegisterUniqueReading_AfterTheWindowExpires_AcceptsItAgain()
    {
        // La ventana es deslizante de verdad: se apoya en el TTL de Redis, no en un temporizador
        // del proceso que se perdería al reiniciar el servicio.
        var cache = new RedisPositionCache(redis.Connection);
        var reading = Reading("VH-TTL-1", 4.7110, -74.0721, Now);
        var shortWindow = TimeSpan.FromMilliseconds(700);

        var first = await cache.TryRegisterUniqueReadingAsync(reading, shortWindow, CancellationToken.None);
        await Task.Delay(TimeSpan.FromSeconds(1.2));
        var afterExpiry = await cache.TryRegisterUniqueReadingAsync(reading, shortWindow, CancellationToken.None);

        first.ShouldBeTrue();
        afterExpiry.ShouldBeTrue();
    }

    [Fact]
    public async Task TryRegisterUniqueReading_WithGpsNoiseBelowThePrecisionThreshold_IsTreatedAsTheSamePoint()
    {
        // Diferencia en la séptima cifra decimal: centímetros. Debe colapsar a la misma clave, o la
        // deduplicación no atraparía ningún reenvío real.
        var cache = new RedisPositionCache(redis.Connection);

        var first = await cache.TryRegisterUniqueReadingAsync(
            Reading("VH-NOISE-1", 4.7110001, -74.0721001, Now), Window, CancellationToken.None);
        var jittered = await cache.TryRegisterUniqueReadingAsync(
            Reading("VH-NOISE-1", 4.7110002, -74.0721002, Now), Window, CancellationToken.None);

        first.ShouldBeTrue();
        jittered.ShouldBeFalse();
    }

    private static TelemetryReading Reading(string vehicleId, double latitude, double longitude, DateTimeOffset at) =>
        TelemetryReading.Create(vehicleId, latitude, longitude, at, at, TelemetryValidationPolicy.Default).Value;
}
