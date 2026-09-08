using System.Globalization;
using FleetTelemetry.Domain.Common;

namespace FleetTelemetry.Domain.Telemetry;

/// <summary>
/// Punto geográfico validado, con la aritmética necesaria para decidir si un vehículo se movió.
/// </summary>
public readonly record struct Coordinate
{
    /// <summary>
    /// Precisión a la que se comparan y se cachean las coordenadas.
    /// </summary>
    /// <remarks>
    /// Cinco decimales equivalen a ~1.1 m en el ecuador. Es el punto donde el ruido del GPS de un
    /// vehículo parado deja de generar coordenadas distintas: sin este redondeo, la deduplicación no
    /// atraparía nada y ningún vehículo parecería jamás detenido.
    /// </remarks>
    public const int ComparisonPrecision = 5;

    private const double EarthRadiusInMeters = 6_371_008.8;

    private Coordinate(double latitude, double longitude)
    {
        Latitude = latitude;
        Longitude = longitude;
    }

    public double Latitude { get; }

    public double Longitude { get; }

    public static Result<Coordinate> Create(double latitude, double longitude)
    {
        if (double.IsNaN(latitude) || double.IsInfinity(latitude) ||
            double.IsNaN(longitude) || double.IsInfinity(longitude))
        {
            return Result.Failure<Coordinate>(TelemetryErrors.CoordinateNotFinite);
        }

        if (latitude is < -90 or > 90)
        {
            return Result.Failure<Coordinate>(TelemetryErrors.LatitudeOutOfRange);
        }

        if (longitude is < -180 or > 180)
        {
            return Result.Failure<Coordinate>(TelemetryErrors.LongitudeOutOfRange);
        }

        return Result.Success(new Coordinate(latitude, longitude));
    }

    /// <summary>
    /// Distancia sobre la superficie terrestre usando la fórmula de Haversine.
    /// </summary>
    /// <remarks>
    /// Haversine y no Vincenty: a las distancias que maneja este sistema —decenas de metros entre
    /// dos lecturas consecutivas— el error del modelo esférico es de centímetros, muy por debajo de
    /// la precisión del propio GPS, y cuesta una fracción del cálculo.
    /// </remarks>
    public double DistanceInMetersTo(Coordinate other)
    {
        var latitudeDelta = ToRadians(other.Latitude - Latitude);
        var longitudeDelta = ToRadians(other.Longitude - Longitude);

        var a = (Math.Sin(latitudeDelta / 2) * Math.Sin(latitudeDelta / 2)) +
                (Math.Cos(ToRadians(Latitude)) * Math.Cos(ToRadians(other.Latitude)) *
                 Math.Sin(longitudeDelta / 2) * Math.Sin(longitudeDelta / 2));

        return 2 * EarthRadiusInMeters * Math.Asin(Math.Min(1.0, Math.Sqrt(a)));
    }

    /// <summary>
    /// Clave estable para deduplicación y caché, redondeada a <see cref="ComparisonPrecision"/>.
    /// </summary>
    public string ToCacheKey() => string.Create(
        CultureInfo.InvariantCulture,
        $"{Math.Round(Latitude, ComparisonPrecision)}:{Math.Round(Longitude, ComparisonPrecision)}");

    private static double ToRadians(double degrees) => degrees * Math.PI / 180.0;
}
