using System.Text.RegularExpressions;
using FleetTelemetry.Domain.Common;
using FleetTelemetry.Domain.Telemetry;

namespace FleetTelemetry.Domain.Vehicles;

/// <summary>
/// Identificador de vehículo validado.
/// </summary>
/// <remarks>
/// Es un tipo propio y no un <c>string</c> porque este valor termina formando claves de Redis
/// (<c>vehicle:{id}:last</c>). Un identificador con ':' o con longitud arbitraria podría colisionar
/// con el espacio de claves de otro vehículo, así que el formato se valida una vez, aquí, y el resto
/// del sistema puede confiar en él.
/// </remarks>
public readonly partial record struct VehicleId
{
    public const int MaxLength = 64;

    private VehicleId(string value) => Value = value;

    public string Value { get; }

    public static Result<VehicleId> Create(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Result.Failure<VehicleId>(TelemetryErrors.VehicleIdEmpty);
        }

        var trimmed = value.Trim();

        if (trimmed.Length > MaxLength)
        {
            return Result.Failure<VehicleId>(TelemetryErrors.VehicleIdTooLong);
        }

        if (!AllowedFormat().IsMatch(trimmed))
        {
            return Result.Failure<VehicleId>(TelemetryErrors.VehicleIdInvalidFormat);
        }

        return Result.Success(new VehicleId(trimmed));
    }

    public override string ToString() => Value;

    [GeneratedRegex(@"^[A-Za-z0-9_-]+$", RegexOptions.CultureInvariant)]
    private static partial Regex AllowedFormat();
}
