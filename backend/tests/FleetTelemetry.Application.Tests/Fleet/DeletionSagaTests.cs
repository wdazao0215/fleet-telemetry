using FleetTelemetry.Application.Abstractions.Ports;
using FleetTelemetry.Application.Fleet.CompleteVehicleDeletion;
using FleetTelemetry.Application.Fleet.DeleteVehicle;
using FleetTelemetry.Application.Tests.Doubles;
using FleetTelemetry.Contracts.Events;
using FleetTelemetry.Domain.Telemetry;
using FleetTelemetry.Domain.Vehicles;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Shouldly;

namespace FleetTelemetry.Application.Tests.Fleet;

/// <summary>
/// La saga de borrado: dos almacenes sin transacción común, con estado observable y compensación.
/// </summary>
public class DeletionSagaTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);

    private readonly IVehicleRepository vehicles = Substitute.For<IVehicleRepository>();
    private readonly IPositionRepository positions = Substitute.For<IPositionRepository>();
    private readonly IAlertRepository alerts = Substitute.For<IAlertRepository>();
    private readonly IPositionCache cache = Substitute.For<IPositionCache>();
    private readonly IEventBus eventBus = Substitute.For<IEventBus>();

    [Fact]
    public async Task DeleteVehicle_MarksItPendingBeforePublishing()
    {
        // El orden es una propiedad de correctitud: publicar antes de guardar podría procesar el
        // borrado de un vehículo cuya transacción luego se revierte.
        var vehicle = GivenRegisteredVehicle();

        var result = await DeleteHandler().HandleAsync(
            new DeleteVehicleCommand("VH-001", "correlation"), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        vehicle.State.ShouldBe(VehicleLifecycleState.PendingDeletion);

        Received.InOrder(() =>
        {
            vehicles.SaveChangesAsync(Arg.Any<CancellationToken>());
            eventBus.PublishAsync(
                Arg.Any<VehicleDeletionRequestedEvent>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        });
    }

    [Fact]
    public async Task DeleteVehicle_StopsAcceptingTelemetryImmediately()
    {
        // Sin esto, una posición en vuelo podría resucitar los datos que la saga está purgando.
        var vehicle = GivenRegisteredVehicle();

        await DeleteHandler().HandleAsync(new DeleteVehicleCommand("VH-001", "c"), CancellationToken.None);

        vehicle.AcceptsTelemetry.ShouldBeFalse();
    }

    [Fact]
    public async Task DeleteVehicle_WhenItDoesNotExist_ReturnsNotFound()
    {
        vehicles.GetAsync(Arg.Any<VehicleId>(), Arg.Any<CancellationToken>()).Returns((Vehicle?)null);

        var result = await DeleteHandler().HandleAsync(
            new DeleteVehicleCommand("VH-404", "c"), CancellationToken.None);

        result.Error.ShouldBe(TelemetryErrors.VehicleNotFound);
    }

    [Fact]
    public async Task DeleteVehicle_RetriesAVehicleWhoseDeletionFailed()
    {
        // Es exactamente el caso para el que existe el estado DeletionFailed.
        var vehicle = GivenRegisteredVehicle();
        vehicle.RequestDeletion(Now);
        vehicle.FailDeletion("Redis no respondió");

        var result = await DeleteHandler().HandleAsync(
            new DeleteVehicleCommand("VH-001", "c"), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        vehicle.State.ShouldBe(VehicleLifecycleState.PendingDeletion);
        vehicle.DeletionFailureReason.ShouldBeNull();
    }

    [Fact]
    public async Task CompleteDeletion_PurgesTheCacheBeforeTheHistory()
    {
        // La caché es lo que lee el dashboard: purgarla primero hace que el vehículo desaparezca de
        // la pantalla del operador de inmediato, aunque borrar millones de filas tarde.
        var vehicle = GivenRegisteredVehicle();
        vehicle.RequestDeletion(Now);

        await CompleteHandler().HandleAsync(
            new CompleteVehicleDeletionCommand("VH-001", "c"), CancellationToken.None);

        Received.InOrder(() =>
        {
            cache.PurgeVehicleAsync(Arg.Any<VehicleId>(), Arg.Any<CancellationToken>());
            positions.DeleteHistoryAsync(Arg.Any<VehicleId>(), Arg.Any<CancellationToken>());
        });
    }

    [Fact]
    public async Task CompleteDeletion_WhenAStepFails_LeavesTheVehicleObservableAndDoesNotReactivateIt()
    {
        // Un borrado a medias no puede volver a Active: ya perdió parte de sus datos y mostrarlo
        // como operativo daría un estado incoherente al operador.
        var vehicle = GivenRegisteredVehicle();
        vehicle.RequestDeletion(Now);

        positions
            .DeleteHistoryAsync(Arg.Any<VehicleId>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("la base de datos no responde"));

        var result = await CompleteHandler().HandleAsync(
            new CompleteVehicleDeletionCommand("VH-001", "c"), CancellationToken.None);

        result.Value.Succeeded.ShouldBeFalse();
        vehicle.State.ShouldBe(VehicleLifecycleState.DeletionFailed);
        vehicle.DeletionFailureReason.ShouldNotBeNullOrWhiteSpace();

        await eventBus.Received(1).PublishAsync(
            Arg.Is<VehicleDeletionCompletedEvent>(e => !e.Succeeded),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CompleteDeletion_OnAVehicleAlreadyGone_SucceedsWithoutFailing()
    {
        // La cola entrega at-least-once: reprocesar el mensaje no puede romper nada.
        vehicles.GetAsync(Arg.Any<VehicleId>(), Arg.Any<CancellationToken>()).Returns((Vehicle?)null);

        var result = await CompleteHandler().HandleAsync(
            new CompleteVehicleDeletionCommand("VH-001", "c"), CancellationToken.None);

        result.Value.Succeeded.ShouldBeTrue();
    }

    private Vehicle GivenRegisteredVehicle()
    {
        var vehicle = Vehicle.Register(VehicleId.Create("VH-001").Value, "Camión 1", Now.AddDays(-1));
        vehicles.GetAsync(Arg.Any<VehicleId>(), Arg.Any<CancellationToken>()).Returns(vehicle);
        return vehicle;
    }

    private DeleteVehicleHandler DeleteHandler() => new(vehicles, eventBus, new FixedClock(Now));

    private CompleteVehicleDeletionHandler CompleteHandler() =>
        new(vehicles, positions, alerts, cache, eventBus, new FixedClock(Now));
}
