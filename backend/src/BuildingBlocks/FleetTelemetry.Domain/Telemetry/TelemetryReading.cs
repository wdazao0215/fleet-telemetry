using FleetTelemetry.Domain.Common;
using FleetTelemetry.Domain.Vehicles;

namespace FleetTelemetry.Domain.Telemetry;

/// <summary>
/// Lectura GPS válida. Si existe una instancia de este tipo, sus invariantes ya se cumplieron.
/// </summary>
/// <remarks>
/// La construcción pasa por <see cref="Create"/> y no por el constructor para que sea imposible
/// tener una lectura a medio validar circulando por el sistema. Es la frontera entre "lo que llegó
/// por la red" y "lo que el dominio acepta como cierto".
/// </remarks>
public sealed record TelemetryReading
{
    private TelemetryReading(VehicleId vehicleId, Coordinate position, DateTimeOffset recordedAt)
    {
        VehicleId = vehicleId;
        Position = position;
        RecordedAt = recordedAt;
    }

    public VehicleId VehicleId { get; }

    public Coordinate Position { get; }

    /// <summary>Momento en que el dispositivo tomó la lectura, no en que el servidor la recibió.</summary>
    /// <remarks>
    /// La diferencia importa con sincronización offline: un lote que llega tras diez minutos en un
    /// túnel trae lecturas viejas que son perfectamente válidas y deben ordenarse por este campo.
    /// </remarks>
    public DateTimeOffset RecordedAt { get; }

    public static Result<TelemetryReading> Create(
        string? vehicleId,
        double latitude,
        double longitude,
        DateTimeOffset recordedAt,
        DateTimeOffset now,
        TelemetryValidationPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);

        var vehicleIdResult = VehicleId.Create(vehicleId);
        if (vehicleIdResult.IsFailure)
        {
            return Result.Failure<TelemetryReading>(vehicleIdResult.Error);
        }

        var positionResult = Coordinate.Create(latitude, longitude);
        if (positionResult.IsFailure)
        {
            return Result.Failure<TelemetryReading>(positionResult.Error);
        }

        var reading = new TelemetryReading(vehicleIdResult.Value, positionResult.Value, recordedAt);

        var specificationResult = policy.SpecificationsAt(now).IsSatisfiedByAll(reading);
        return specificationResult.IsFailure
            ? Result.Failure<TelemetryReading>(specificationResult.Error)
            : Result.Success(reading);
    }
}
