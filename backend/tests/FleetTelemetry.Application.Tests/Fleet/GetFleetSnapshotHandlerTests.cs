using FleetTelemetry.Application.Abstractions.Models;
using FleetTelemetry.Application.Abstractions.Ports;
using FleetTelemetry.Application.Configuration;
using FleetTelemetry.Application.Fleet.GetFleetSnapshot;
using FleetTelemetry.Application.Tests.Doubles;
using FleetTelemetry.Domain.Telemetry;
using FleetTelemetry.Domain.Vehicles;
using NSubstitute;
using Shouldly;

namespace FleetTelemetry.Application.Tests.Fleet;

public class GetFleetSnapshotHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);
    private static readonly Coordinate Somewhere = Coordinate.Create(4.7110, -74.0721).Value;

    private readonly IVehicleRepository vehicles = Substitute.For<IVehicleRepository>();
    private readonly IAlertRepository alerts = Substitute.For<IAlertRepository>();
    private readonly IPositionCache cache = Substitute.For<IPositionCache>();

    [Fact]
    public async Task HandleAsync_WithAVehicleThatHasNoCachedPosition_ReportsItAsOffline()
    {
        // Caso real: el vehículo está en la base pero su estado en caché caducó porque lleva rato
        // sin emitir. Debe aparecer en el listado, no desaparecer de él.
        GivenRegistered("VH-001");
        GivenNoLivePositions();
        GivenNoAlerts();

        var snapshot = await Handler().HandleAsync(new GetFleetSnapshotQuery(), CancellationToken.None);

        snapshot.Value.Single().Status.ShouldBe(VehicleActivityStatus.Offline);
        snapshot.Value.Single().Latitude.ShouldBeNull();
    }

    [Fact]
    public async Task HandleAsync_WithARecentlyMovedVehicle_ReportsItAsMoving()
    {
        GivenRegistered("VH-001");
        GivenLivePosition("VH-001", Now.AddSeconds(-2), movedAt: Now.AddSeconds(-2));
        GivenNoAlerts();

        var snapshot = await Handler().HandleAsync(new GetFleetSnapshotQuery(), CancellationToken.None);

        snapshot.Value.Single().Status.ShouldBe(VehicleActivityStatus.Moving);
    }

    [Fact]
    public async Task HandleAsync_WithAnUnacknowledgedAlert_ReportsItAsAlerted()
    {
        GivenRegistered("VH-001");
        GivenLivePosition("VH-001", Now.AddSeconds(-2), movedAt: Now.AddSeconds(-2));
        GivenAlertsFor("VH-001");

        var snapshot = await Handler().HandleAsync(new GetFleetSnapshotQuery(), CancellationToken.None);

        snapshot.Value.Single().Status.ShouldBe(VehicleActivityStatus.Alerted);
        snapshot.Value.Single().HasUnacknowledgedAlert.ShouldBeTrue();
    }

    [Fact]
    public async Task HandleAsync_ExposesStationarySinceSoTheDashboardCanShowATimer()
    {
        GivenRegistered("VH-001");
        GivenLivePosition("VH-001", Now.AddSeconds(-2), movedAt: Now.AddMinutes(-4));
        GivenNoAlerts();

        var snapshot = await Handler().HandleAsync(new GetFleetSnapshotQuery(), CancellationToken.None);

        snapshot.Value.Single().StationarySince.ShouldBe(Now.AddMinutes(-4));
    }

    private GetFleetSnapshotHandler Handler() =>
        new(vehicles, alerts, cache, new FixedClock(Now), new TelemetryOptions());

    private void GivenRegistered(params string[] ids) => vehicles
        .ListAsync(Arg.Any<bool>(), Arg.Any<CancellationToken>())
        .Returns([.. ids.Select(id => Vehicle.Register(VehicleId.Create(id).Value, null, Now.AddDays(-1)))]);

    private void GivenNoLivePositions() => cache
        .GetLiveStateAsync(Arg.Any<CancellationToken>())
        .Returns([]);

    private void GivenLivePosition(string id, DateTimeOffset seenAt, DateTimeOffset movedAt) => cache
        .GetLiveStateAsync(Arg.Any<CancellationToken>())
        .Returns([
            new LivePosition(
                VehicleId.Create(id).Value, Somewhere, seenAt, new MovementSnapshot(Somewhere, movedAt)),
        ]);

    private void GivenNoAlerts() => alerts
        .ListVehiclesWithUnacknowledgedAlertsAsync(Arg.Any<CancellationToken>())
        .Returns([]);

    private void GivenAlertsFor(string id) => alerts
        .ListVehiclesWithUnacknowledgedAlertsAsync(Arg.Any<CancellationToken>())
        .Returns([VehicleId.Create(id).Value]);
}
