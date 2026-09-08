using FleetTelemetry.Domain.Common;

namespace FleetTelemetry.Domain.Telemetry;

/// <summary>
/// Catálogo de errores de telemetría. Centralizarlos evita códigos duplicados o divergentes.
/// </summary>
public static class TelemetryErrors
{
    public static readonly Error LatitudeOutOfRange =
        Error.Validation("telemetry.latitude_out_of_range", "La latitud debe estar entre -90 y 90 grados.");

    public static readonly Error LongitudeOutOfRange =
        Error.Validation("telemetry.longitude_out_of_range", "La longitud debe estar entre -180 y 180 grados.");

    public static readonly Error CoordinateNotFinite =
        Error.Validation("telemetry.coordinate_not_finite", "La coordenada debe ser un número finito.");

    public static readonly Error TimestampInFuture =
        Error.Validation("telemetry.timestamp_in_future", "El timestamp no puede estar en el futuro.");

    public static readonly Error TimestampTooOld =
        Error.Validation("telemetry.timestamp_too_old", "El timestamp excede la antigüedad admitida.");

    public static readonly Error VehicleIdEmpty =
        Error.Validation("vehicle.id_empty", "El identificador de vehículo es obligatorio.");

    public static readonly Error VehicleIdTooLong =
        Error.Validation("vehicle.id_too_long", "El identificador de vehículo excede la longitud máxima.");

    public static readonly Error VehicleIdInvalidFormat =
        Error.Validation("vehicle.id_invalid_format", "El identificador de vehículo tiene caracteres no permitidos.");

    public static readonly Error VehicleNotFound =
        Error.NotFound("vehicle.not_found", "El vehículo no existe.");

    public static readonly Error VehicleAlreadyBeingDeleted =
        Error.Conflict("vehicle.already_being_deleted", "El vehículo ya está en proceso de eliminación.");
}
