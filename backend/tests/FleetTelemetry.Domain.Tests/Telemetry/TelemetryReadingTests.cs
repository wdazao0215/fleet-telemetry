using FleetTelemetry.Domain.Telemetry;
using Shouldly;

namespace FleetTelemetry.Domain.Tests.Telemetry;

public class TelemetryReadingTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);
    private static readonly TelemetryValidationPolicy Policy =
        new(clockSkewTolerance: TimeSpan.FromMinutes(2), maximumAge: TimeSpan.FromHours(24));

    [Fact]
    public void Create_WithAValidPayload_Succeeds()
    {
        var result = TelemetryReading.Create("VH-001", 4.7110, -74.0721, Now, Now, Policy);

        result.IsSuccess.ShouldBeTrue();
        result.Value.VehicleId.Value.ShouldBe("VH-001");
    }

    [Fact]
    public void Create_WhenTimestampIsBeyondTheClockSkewTolerance_Fails()
    {
        var result = TelemetryReading.Create("VH-001", 4.7110, -74.0721, Now.AddMinutes(3), Now, Policy);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(TelemetryErrors.TimestampInFuture);
    }

    [Fact]
    public void Create_WhenTimestampIsWithinTheClockSkewTolerance_Succeeds()
    {
        // Un móvil con el reloj un minuto adelantado es normal. Rechazarlo haría que el conductor
        // viese un fallo de red que no existe.
        var result = TelemetryReading.Create("VH-001", 4.7110, -74.0721, Now.AddMinutes(1), Now, Policy);

        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void Create_WithAnOldButAdmissibleTimestamp_Succeeds()
    {
        // Lote de sincronización tras diez minutos sin cobertura: viejo, pero legítimo.
        var result = TelemetryReading.Create("VH-001", 4.7110, -74.0721, Now.AddMinutes(-10), Now, Policy);

        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void Create_WhenTimestampExceedsTheMaximumAge_Fails()
    {
        var result = TelemetryReading.Create("VH-001", 4.7110, -74.0721, Now.AddDays(-2), Now, Policy);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(TelemetryErrors.TimestampTooOld);
    }

    [Fact]
    public void Create_WithAnInvalidLatitude_FailsBeforeCheckingTheTimestamp()
    {
        // El simulador inyecta un 5% de payloads malformados; el error devuelto debe señalar el
        // campo que realmente está mal, no el siguiente que se validaría.
        var result = TelemetryReading.Create("VH-001", 999, -74.0721, Now.AddDays(-2), Now, Policy);

        result.Error.ShouldBe(TelemetryErrors.LatitudeOutOfRange);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithoutAVehicleId_Fails(string? vehicleId)
    {
        var result = TelemetryReading.Create(vehicleId, 4.7110, -74.0721, Now, Now, Policy);

        result.Error.ShouldBe(TelemetryErrors.VehicleIdEmpty);
    }

    [Fact]
    public void Create_WithAVehicleIdContainingCacheKeySeparators_Fails()
    {
        // 'VH:001' produciría la clave 'vehicle:VH:001:last', que puede colisionar con el espacio de
        // claves de otro vehículo. Se rechaza en el borde y no en la caché.
        var result = TelemetryReading.Create("VH:001", 4.7110, -74.0721, Now, Now, Policy);

        result.Error.ShouldBe(TelemetryErrors.VehicleIdInvalidFormat);
    }
}
