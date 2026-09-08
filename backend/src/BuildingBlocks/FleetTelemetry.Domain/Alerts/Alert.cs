using FleetTelemetry.Domain.Common;
using FleetTelemetry.Domain.Telemetry;
using FleetTelemetry.Domain.Vehicles;

namespace FleetTelemetry.Domain.Alerts;

/// <summary>
/// Anomalía detectada sobre un vehículo.
/// </summary>
public sealed class Alert
{
    private Alert()
    {
        Detail = string.Empty;
    }

    private Alert(
        Guid id,
        VehicleId vehicleId,
        AlertKind kind,
        Coordinate position,
        DateTimeOffset raisedAt,
        string detail)
    {
        Id = id;
        VehicleId = vehicleId;
        Kind = kind;
        Position = position;
        RaisedAt = raisedAt;
        Detail = detail;
    }

    public Guid Id { get; private set; }

    public VehicleId VehicleId { get; private set; }

    public AlertKind Kind { get; private set; }

    public Coordinate Position { get; private set; }

    public DateTimeOffset RaisedAt { get; private set; }

    public DateTimeOffset? AcknowledgedAt { get; private set; }

    public string Detail { get; private set; }

    public bool IsAcknowledged => AcknowledgedAt is not null;

    public static Alert StoppedVehicle(
        VehicleId vehicleId,
        Coordinate position,
        DateTimeOffset raisedAt,
        TimeSpan stationaryFor) =>
        new(
            Guid.CreateVersion7(),
            vehicleId,
            AlertKind.StoppedVehicle,
            position,
            raisedAt,
            $"Vehículo detenido durante {stationaryFor.TotalSeconds:F0} segundos.");

    public static Alert Panic(VehicleId vehicleId, Coordinate position, DateTimeOffset raisedAt) =>
        new(
            Guid.CreateVersion7(),
            vehicleId,
            AlertKind.PanicButton,
            position,
            raisedAt,
            "Botón de pánico activado por el conductor.");

    public Result Acknowledge(DateTimeOffset acknowledgedAt)
    {
        if (IsAcknowledged)
        {
            return Result.Failure(Error.Conflict("alert.already_acknowledged", "La alerta ya fue atendida."));
        }

        AcknowledgedAt = acknowledgedAt;
        return Result.Success();
    }
}
