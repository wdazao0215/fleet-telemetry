using FleetTelemetry.Domain.Telemetry;
using Shouldly;

namespace FleetTelemetry.Domain.Tests.Telemetry;

/// <summary>
/// Regla central del enunciado: "si un vehículo envía la misma coordenada durante más de un minuto,
/// debe generar una alerta de Vehículo Detenido". Los bordes importan más que el caso feliz.
/// </summary>
public class StoppedVehicleDetectorTests
{
    private static readonly DateTimeOffset Origin = new(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);
    private static readonly StoppedVehiclePolicy Policy = new(radiusInMeters: 10, threshold: TimeSpan.FromMinutes(1));

    [Fact]
    public void Evaluate_WithNoHistory_TreatsTheReadingAsMovementAndNeverAlerts()
    {
        var evaluation = StoppedVehicleDetector.Evaluate(lastMovement: null, ReadingAt(Origin), Policy);

        evaluation.HasMoved.ShouldBeTrue();
        evaluation.IsStopped.ShouldBeFalse();
        evaluation.LastMovement.ObservedAt.ShouldBe(Origin);
    }

    [Fact]
    public void Evaluate_WhenTheVehicleLeavesTheRadius_ResetsTheStopwatch()
    {
        var anchor = new MovementSnapshot(Point(4.7110, -74.0721), Origin);

        // ~110 m al norte: claramente fuera del radio de 10 m.
        var evaluation = StoppedVehicleDetector.Evaluate(
            anchor, ReadingAt(Origin.AddMinutes(5), latitude: 4.7120), Policy);

        evaluation.HasMoved.ShouldBeTrue();
        evaluation.IsStopped.ShouldBeFalse();
        evaluation.StationaryFor.ShouldBe(TimeSpan.Zero);
        evaluation.LastMovement.ObservedAt.ShouldBe(Origin.AddMinutes(5));
    }

    [Fact]
    public void Evaluate_WhenStationaryForExactlyTheThreshold_Alerts()
    {
        // El enunciado dice "más de un minuto". El borde exacto se define como alerta: un vehículo
        // parado 60.000 s no debería quedar en un limbo silencioso por un milisegundo.
        var anchor = new MovementSnapshot(Point(4.7110, -74.0721), Origin);

        var evaluation = StoppedVehicleDetector.Evaluate(anchor, ReadingAt(Origin.AddMinutes(1)), Policy);

        evaluation.IsStopped.ShouldBeTrue();
        evaluation.StationaryFor.ShouldBe(TimeSpan.FromMinutes(1));
    }

    [Fact]
    public void Evaluate_WhenStationaryForJustUnderTheThreshold_DoesNotAlert()
    {
        var anchor = new MovementSnapshot(Point(4.7110, -74.0721), Origin);

        var evaluation = StoppedVehicleDetector.Evaluate(
            anchor, ReadingAt(Origin.AddSeconds(59.999)), Policy);

        evaluation.IsStopped.ShouldBeFalse();
    }

    [Fact]
    public void Evaluate_WhenDriftingWithinTheRadius_KeepsTheOriginalAnchor()
    {
        // El caso que rompe una implementación ingenua: el GPS de un vehículo parado deriva unos
        // metros en cada lectura. Si el ancla se actualizase con cada una, el cronómetro se
        // reiniciaría siempre y la alerta no llegaría nunca.
        var anchor = new MovementSnapshot(Point(4.7110, -74.0721), Origin);
        var driftedSlightly = ReadingAt(Origin.AddSeconds(30), latitude: 4.71104);

        var evaluation = StoppedVehicleDetector.Evaluate(anchor, driftedSlightly, Policy);

        evaluation.HasMoved.ShouldBeFalse();
        evaluation.LastMovement.ObservedAt.ShouldBe(Origin);

        var oneMinuteIn = StoppedVehicleDetector.Evaluate(
            evaluation.LastMovement, ReadingAt(Origin.AddSeconds(61), latitude: 4.71108), Policy);

        oneMinuteIn.IsStopped.ShouldBeTrue();
    }

    [Fact]
    public void Evaluate_WithAnOutOfOrderReading_DoesNotReportNegativeTime()
    {
        // Al drenar una cola offline las lecturas pueden llegar desordenadas. Una duración negativa
        // haría que la comparación con el umbral fuese impredecible.
        var anchor = new MovementSnapshot(Point(4.7110, -74.0721), Origin);

        var evaluation = StoppedVehicleDetector.Evaluate(anchor, ReadingAt(Origin.AddSeconds(-30)), Policy);

        evaluation.StationaryFor.ShouldBe(TimeSpan.Zero);
        evaluation.IsStopped.ShouldBeFalse();
    }

    private static Coordinate Point(double latitude, double longitude) =>
        Coordinate.Create(latitude, longitude).Value;

    private static TelemetryReading ReadingAt(
        DateTimeOffset recordedAt,
        double latitude = 4.7110,
        double longitude = -74.0721) =>
        TelemetryReading.Create(
            "VH-001", latitude, longitude, recordedAt,
            now: recordedAt, TelemetryValidationPolicy.Default).Value;
}
