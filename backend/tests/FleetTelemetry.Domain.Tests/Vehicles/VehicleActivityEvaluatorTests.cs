using FleetTelemetry.Domain.Telemetry;
using FleetTelemetry.Domain.Vehicles;
using Shouldly;

namespace FleetTelemetry.Domain.Tests.Vehicles;

public class VehicleActivityEvaluatorTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);
    private static readonly StoppedVehiclePolicy Policy = StoppedVehiclePolicy.Default;
    private static readonly Coordinate Somewhere = Coordinate.Create(4.7110, -74.0721).Value;

    [Fact]
    public void Resolve_WithoutAnyReading_IsOffline()
    {
        var status = VehicleActivityEvaluator.Resolve(null, null, false, Now, Policy);

        status.ShouldBe(VehicleActivityStatus.Offline);
    }

    [Fact]
    public void Resolve_WhenTheLastReadingIsStale_IsOffline()
    {
        var status = VehicleActivityEvaluator.Resolve(
            lastSeenAt: Now.AddMinutes(-1), lastMovement: null, hasUnacknowledgedAlert: false, Now, Policy);

        status.ShouldBe(VehicleActivityStatus.Offline);
    }

    [Fact]
    public void Resolve_WhenMovingRecently_IsMoving()
    {
        var lastSeen = Now.AddSeconds(-3);

        var status = VehicleActivityEvaluator.Resolve(
            lastSeen, new MovementSnapshot(Somewhere, lastSeen), false, Now, Policy);

        status.ShouldBe(VehicleActivityStatus.Moving);
    }

    [Fact]
    public void Resolve_WhenStationaryBelowTheThreshold_IsStopped()
    {
        var lastSeen = Now.AddSeconds(-3);

        var status = VehicleActivityEvaluator.Resolve(
            lastSeen, new MovementSnapshot(Somewhere, lastSeen.AddSeconds(-20)), false, Now, Policy);

        status.ShouldBe(VehicleActivityStatus.Stopped);
    }

    [Fact]
    public void Resolve_WhenStationaryBeyondTheThreshold_IsAlerted()
    {
        var lastSeen = Now.AddSeconds(-3);

        var status = VehicleActivityEvaluator.Resolve(
            lastSeen, new MovementSnapshot(Somewhere, lastSeen.AddMinutes(-2)), false, Now, Policy);

        status.ShouldBe(VehicleActivityStatus.Alerted);
    }

    [Fact]
    public void Resolve_WithAnUnacknowledgedAlert_TakesPrecedenceOverBeingOffline()
    {
        // Un vehículo que disparó el botón de pánico y perdió cobertura es exactamente el que no
        // puede desaparecer del panel entre los "desconectados".
        var status = VehicleActivityEvaluator.Resolve(
            lastSeenAt: Now.AddHours(-1), lastMovement: null, hasUnacknowledgedAlert: true, Now, Policy);

        status.ShouldBe(VehicleActivityStatus.Alerted);
    }
}
