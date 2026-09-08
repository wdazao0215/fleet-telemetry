using FleetTelemetry.Domain.Common;
using FleetTelemetry.Domain.Telemetry;

namespace FleetTelemetry.Domain.Vehicles;

/// <summary>
/// Vehículo de la flota y guardián de las transiciones válidas de su ciclo de vida.
/// </summary>
public sealed class Vehicle
{
    // EF Core materializa las entidades sin pasar por los métodos de fábrica. Es la única concesión
    // que el dominio le hace al ORM: un constructor sin parámetros que nadie más debería usar.
    private Vehicle()
    {
        Label = string.Empty;
    }

    private Vehicle(VehicleId id, string label, DateTimeOffset registeredAt)
    {
        Id = id;
        Label = label;
        RegisteredAt = registeredAt;
        State = VehicleLifecycleState.Active;
    }

    public VehicleId Id { get; private set; }

    public string Label { get; private set; }

    public VehicleLifecycleState State { get; private set; }

    public DateTimeOffset RegisteredAt { get; private set; }

    public DateTimeOffset? DeletionRequestedAt { get; private set; }

    public string? DeletionFailureReason { get; private set; }

    public static Vehicle Register(VehicleId id, string? label, DateTimeOffset registeredAt) =>
        new(id, string.IsNullOrWhiteSpace(label) ? id.Value : label.Trim(), registeredAt);

    /// <summary>Primer paso de la saga: el vehículo deja de aceptarse como activo.</summary>
    public Result RequestDeletion(DateTimeOffset requestedAt)
    {
        if (State is VehicleLifecycleState.PendingDeletion)
        {
            return Result.Failure(TelemetryErrors.VehicleAlreadyBeingDeleted);
        }

        if (State is VehicleLifecycleState.Deleted)
        {
            return Result.Failure(TelemetryErrors.VehicleNotFound);
        }

        State = VehicleLifecycleState.PendingDeletion;
        DeletionRequestedAt = requestedAt;
        DeletionFailureReason = null;
        return Result.Success();
    }

    /// <summary>Último paso: la caché quedó limpia y el histórico purgado.</summary>
    public Result ConfirmDeletion()
    {
        if (State is not VehicleLifecycleState.PendingDeletion)
        {
            return Result.Failure(Error.Conflict(
                "vehicle.invalid_transition",
                $"No se puede confirmar la eliminación desde el estado {State}."));
        }

        State = VehicleLifecycleState.Deleted;
        DeletionFailureReason = null;
        return Result.Success();
    }

    /// <summary>
    /// Compensación: un paso de la saga falló tras agotar reintentos.
    /// </summary>
    /// <remarks>
    /// No se revierte a Active. El vehículo puede haber perdido ya parte de su caché, así que
    /// devolverlo a la operación normal mostraría datos incoherentes; queda marcado y visible para
    /// que un operador o un reintento automático lo resuelva.
    /// </remarks>
    public Result FailDeletion(string reason)
    {
        if (State is not VehicleLifecycleState.PendingDeletion)
        {
            return Result.Failure(Error.Conflict(
                "vehicle.invalid_transition",
                $"No se puede marcar como fallida una eliminación en estado {State}."));
        }

        State = VehicleLifecycleState.DeletionFailed;
        DeletionFailureReason = reason;
        return Result.Success();
    }

    public Result RetryDeletion(DateTimeOffset requestedAt)
    {
        if (State is not VehicleLifecycleState.DeletionFailed)
        {
            return Result.Failure(Error.Conflict(
                "vehicle.invalid_transition",
                $"Solo se puede reintentar una eliminación fallida; el estado actual es {State}."));
        }

        State = VehicleLifecycleState.PendingDeletion;
        DeletionRequestedAt = requestedAt;
        DeletionFailureReason = null;
        return Result.Success();
    }

    /// <summary>Un vehículo en proceso de borrado ya no debe aceptar telemetría nueva.</summary>
    public bool AcceptsTelemetry => State is VehicleLifecycleState.Active;
}
