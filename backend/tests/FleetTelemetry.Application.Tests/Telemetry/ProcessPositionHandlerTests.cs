using FleetTelemetry.Application.Abstractions.Models;
using FleetTelemetry.Application.Abstractions.Ports;
using FleetTelemetry.Application.Configuration;
using FleetTelemetry.Application.Telemetry.ProcessPosition;
using FleetTelemetry.Application.Tests.Doubles;
using FleetTelemetry.Contracts.Events;
using FleetTelemetry.Domain.Alerts;
using FleetTelemetry.Domain.Telemetry;
using FleetTelemetry.Domain.Vehicles;
using NSubstitute;
using Shouldly;

namespace FleetTelemetry.Application.Tests.Telemetry;

public class ProcessPositionHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);
    private static readonly Coordinate Parked = Coordinate.Create(4.6500, -74.1000).Value;

    private readonly IPositionRepository positions = Substitute.For<IPositionRepository>();
    private readonly IVehicleRepository vehicles = Substitute.For<IVehicleRepository>();
    private readonly IAlertRepository alerts = Substitute.For<IAlertRepository>();
    private readonly IPositionCache cache = Substitute.For<IPositionCache>();
    private readonly IEventBus eventBus = Substitute.For<IEventBus>();
    private readonly TelemetryOptions options = new();

    public ProcessPositionHandlerTests() => GivenAnActiveVehicle();

    [Fact]
    public async Task HandleAsync_WithAMovingVehicle_PersistsThePositionAndRaisesNoAlert()
    {
        GivenTheAnchorIs(Parked, Now.AddMinutes(-5));

        // ~1.1 km al norte del ancla: se movió sin ninguna duda.
        var result = await Handler().HandleAsync(CommandAt(4.6600, -74.1000), CancellationToken.None);

        result.Value.AlertRaised.ShouldBeFalse();
        await positions.Received(1).SaveAsync(Arg.Any<TelemetryReading>(), Arg.Any<CancellationToken>());
        await cache.Received(1).SaveLiveStateAsync(
            Arg.Any<LivePosition>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_WhenStationaryBeyondTheThreshold_RaisesAndPublishesTheAlert()
    {
        GivenTheAnchorIs(Parked, Now.AddSeconds(-70));
        GivenTheAlertCooldownIsFree();

        var result = await Handler().HandleAsync(CommandAt(4.6500, -74.1000), CancellationToken.None);

        result.Value.AlertRaised.ShouldBeTrue();

        await alerts.Received(1).AddAsync(
            Arg.Is<Alert>(alert => alert.Kind == AlertKind.StoppedVehicle), Arg.Any<CancellationToken>());

        // La alerta se publica para que el dashboard la reciba en vivo, no solo se guarda.
        await eventBus.Received(1).PublishAsync(
            Arg.Any<AlertRaisedEvent>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_WhenStationaryButWithinTheCooldown_DoesNotRaiseADuplicateAlert()
    {
        // Un vehículo parado cumple la condición en cada lectura, cada 2-5 segundos. Sin cooldown,
        // un camión aparcado inundaría el panel de alertas idénticas.
        GivenTheAnchorIs(Parked, Now.AddSeconds(-70));
        GivenTheAlertCooldownIsActive();

        var result = await Handler().HandleAsync(CommandAt(4.6500, -74.1000), CancellationToken.None);

        result.Value.AlertRaised.ShouldBeFalse();
        await alerts.DidNotReceiveWithAnyArgs().AddAsync(default!, default);
    }

    [Fact]
    public async Task HandleAsync_WhenStationaryButUnderTheThreshold_DoesNotRaiseAnAlert()
    {
        GivenTheAnchorIs(Parked, Now.AddSeconds(-30));
        GivenTheAlertCooldownIsFree();

        var result = await Handler().HandleAsync(CommandAt(4.6500, -74.1000), CancellationToken.None);

        result.Value.AlertRaised.ShouldBeFalse();
    }

    [Fact]
    public async Task HandleAsync_WhenTheVehicleIsBeingDeleted_DropsThePositionWithoutPersisting()
    {
        // Una posición en vuelo puede llegar después de haberse pedido el borrado. Persistirla
        // resucitaría datos que la saga acaba de purgar.
        GivenAVehicleBeingDeleted();

        var result = await Handler().HandleAsync(CommandAt(4.6500, -74.1000), CancellationToken.None);

        result.Value.Skipped.ShouldBeTrue();
        await positions.DidNotReceiveWithAnyArgs().SaveAsync(default!, default);
        await cache.DidNotReceiveWithAnyArgs().SaveLiveStateAsync(default!, default, default);
    }

    [Fact]
    public async Task HandleAsync_ForAVehicleGivenUpForDeletion_DropsThePosition()
    {
        // El dispositivo de un vehículo dado de baja puede seguir encendido durante días. Sus
        // lecturas no deben resucitarlo ni volver a llenar el histórico que la saga acaba de purgar.
        GivenATombstonedVehicle();

        var result = await Handler().HandleAsync(CommandAt(4.6500, -74.1000), CancellationToken.None);

        result.Value.Skipped.ShouldBeTrue();
        await positions.DidNotReceiveWithAnyArgs().SaveAsync(default!, default);
    }

    [Fact]
    public async Task HandleAsync_PersistsBeforeUpdatingTheCache()
    {
        // El orden importa: si la caché se escribiera primero y la persistencia fallara, el
        // dashboard mostraría una posición que no existe en el histórico.
        GivenTheAnchorIs(Parked, Now.AddMinutes(-5));

        await Handler().HandleAsync(CommandAt(4.6600, -74.1000), CancellationToken.None);

        Received.InOrder(() =>
        {
            positions.SaveAsync(Arg.Any<TelemetryReading>(), Arg.Any<CancellationToken>());
            cache.SaveLiveStateAsync(Arg.Any<LivePosition>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>());
        });
    }

    private ProcessPositionHandler Handler() =>
        new(positions, vehicles, alerts, cache, eventBus, new FixedClock(Now), options);

    private static ProcessPositionCommand CommandAt(double latitude, double longitude) =>
        new("VH-PARKED", latitude, longitude, Now, "correlation");

    private void GivenAnActiveVehicle() => vehicles
        .EnsureRegisteredAsync(Arg.Any<VehicleId>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
        .Returns(Vehicle.Register(VehicleId.Create("VH-PARKED").Value, "Camión", Now.AddDays(-1)));

    private void GivenAVehicleBeingDeleted()
    {
        var vehicle = Vehicle.Register(VehicleId.Create("VH-PARKED").Value, "Camión", Now.AddDays(-1));
        vehicle.RequestDeletion(Now);

        vehicles
            .EnsureRegisteredAsync(Arg.Any<VehicleId>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(vehicle);
    }

    private void GivenATombstonedVehicle()
    {
        var vehicle = Vehicle.Register(VehicleId.Create("VH-PARKED").Value, "Camión", Now.AddDays(-1));
        vehicle.RequestDeletion(Now);
        vehicle.ConfirmDeletion();

        vehicles
            .EnsureRegisteredAsync(Arg.Any<VehicleId>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(vehicle);
    }

    private void GivenTheAnchorIs(Coordinate position, DateTimeOffset observedAt) => cache
        .GetLastMovementAsync(Arg.Any<VehicleId>(), Arg.Any<CancellationToken>())
        .Returns(new MovementSnapshot(position, observedAt));

    private void GivenTheAlertCooldownIsFree() => cache
        .TryMarkAlertRaisedAsync(
            Arg.Any<VehicleId>(), Arg.Any<AlertKind>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
        .Returns(true);

    private void GivenTheAlertCooldownIsActive() => cache
        .TryMarkAlertRaisedAsync(
            Arg.Any<VehicleId>(), Arg.Any<AlertKind>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
        .Returns(false);
}
