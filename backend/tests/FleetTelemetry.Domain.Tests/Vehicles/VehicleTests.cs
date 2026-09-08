using FleetTelemetry.Domain.Telemetry;
using FleetTelemetry.Domain.Vehicles;
using Shouldly;

namespace FleetTelemetry.Domain.Tests.Vehicles;

/// <summary>
/// Transiciones de la saga de eliminación. Son las que garantizan que un borrado a medias quede
/// visible en lugar de dejar un vehículo fantasma en la caché.
/// </summary>
public class VehicleTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Register_WithoutALabel_FallsBackToTheIdentifier()
    {
        var vehicle = Vehicle.Register(Id("VH-001"), label: null, Now);

        vehicle.Label.ShouldBe("VH-001");
        vehicle.State.ShouldBe(VehicleLifecycleState.Active);
        vehicle.AcceptsTelemetry.ShouldBeTrue();
    }

    [Fact]
    public void RequestDeletion_OnAnActiveVehicle_StopsAcceptingTelemetry()
    {
        var vehicle = Vehicle.Register(Id("VH-001"), "Camión 1", Now);

        var result = vehicle.RequestDeletion(Now);

        result.IsSuccess.ShouldBeTrue();
        vehicle.State.ShouldBe(VehicleLifecycleState.PendingDeletion);
        vehicle.AcceptsTelemetry.ShouldBeFalse();
    }

    [Fact]
    public void RequestDeletion_Twice_IsRejected()
    {
        // Dos clics en el botón de borrar no deben lanzar dos sagas para el mismo vehículo.
        var vehicle = Vehicle.Register(Id("VH-001"), "Camión 1", Now);
        vehicle.RequestDeletion(Now);

        var result = vehicle.RequestDeletion(Now.AddSeconds(1));

        result.Error.ShouldBe(TelemetryErrors.VehicleAlreadyBeingDeleted);
    }

    [Fact]
    public void ConfirmDeletion_WithoutHavingRequestedIt_IsRejected()
    {
        var vehicle = Vehicle.Register(Id("VH-001"), "Camión 1", Now);

        var result = vehicle.ConfirmDeletion();

        result.IsFailure.ShouldBeTrue();
        vehicle.State.ShouldBe(VehicleLifecycleState.Active);
    }

    [Fact]
    public void FailDeletion_KeepsTheVehicleOutOfService_AndRecordsWhy()
    {
        // No vuelve a Active: puede haber perdido ya parte de su caché, y reactivarlo mostraría un
        // vehículo con datos incoherentes en el dashboard.
        var vehicle = Vehicle.Register(Id("VH-001"), "Camión 1", Now);
        vehicle.RequestDeletion(Now);

        var result = vehicle.FailDeletion("Redis no respondió tras 3 reintentos");

        result.IsSuccess.ShouldBeTrue();
        vehicle.State.ShouldBe(VehicleLifecycleState.DeletionFailed);
        vehicle.AcceptsTelemetry.ShouldBeFalse();
        vehicle.DeletionFailureReason.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public void RetryDeletion_AfterAFailure_ReopensTheSagaAndClearsTheReason()
    {
        var vehicle = Vehicle.Register(Id("VH-001"), "Camión 1", Now);
        vehicle.RequestDeletion(Now);
        vehicle.FailDeletion("Redis no respondió");

        var result = vehicle.RetryDeletion(Now.AddMinutes(5));

        result.IsSuccess.ShouldBeTrue();
        vehicle.State.ShouldBe(VehicleLifecycleState.PendingDeletion);
        vehicle.DeletionFailureReason.ShouldBeNull();
    }

    [Fact]
    public void RetryDeletion_OnAVehicleThatNeverFailed_IsRejected()
    {
        var vehicle = Vehicle.Register(Id("VH-001"), "Camión 1", Now);

        var result = vehicle.RetryDeletion(Now);

        result.IsFailure.ShouldBeTrue();
    }

    [Fact]
    public void CompleteDeletionFlow_EndsInDeleted()
    {
        var vehicle = Vehicle.Register(Id("VH-001"), "Camión 1", Now);

        vehicle.RequestDeletion(Now);
        vehicle.ConfirmDeletion();

        vehicle.State.ShouldBe(VehicleLifecycleState.Deleted);
        vehicle.DeletionFailureReason.ShouldBeNull();
    }

    private static VehicleId Id(string value) => VehicleId.Create(value).Value;
}
