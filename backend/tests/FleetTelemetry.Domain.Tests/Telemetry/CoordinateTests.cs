using FleetTelemetry.Domain.Telemetry;
using Shouldly;

namespace FleetTelemetry.Domain.Tests.Telemetry;

public class CoordinateTests
{
    [Theory]
    [InlineData(91, 0)]
    [InlineData(-91, 0)]
    [InlineData(0, 181)]
    [InlineData(0, -181)]
    public void Create_WhenOutOfRange_Fails(double latitude, double longitude)
    {
        var result = Coordinate.Create(latitude, longitude);

        result.IsFailure.ShouldBeTrue();
    }

    [Fact]
    public void Create_WhenLatitudeIsNaN_Fails()
    {
        // Un payload con "latitude": null o con texto llega deserializado como NaN. Si pasara, la
        // fórmula de Haversine devolvería NaN y el vehículo nunca se movería ni se detendría.
        var result = Coordinate.Create(double.NaN, 0);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(TelemetryErrors.CoordinateNotFinite);
    }

    [Theory]
    [InlineData(90, 180)]
    [InlineData(-90, -180)]
    [InlineData(0, 0)]
    public void Create_AtTheExactBoundary_Succeeds(double latitude, double longitude)
    {
        var result = Coordinate.Create(latitude, longitude);

        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void DistanceInMetersTo_BetweenTwoKnownPoints_MatchesReference()
    {
        // Plaza de Bolívar (Bogotá) y un punto un grado de longitud al este. A esa latitud, un grado
        // son ~111.0 km; el valor de referencia se calcula con la fórmula de Haversine estándar.
        var origin = Coordinate.Create(4.5981, -74.0758).Value;
        var oneDegreeEast = Coordinate.Create(4.5981, -73.0758).Value;

        var distance = origin.DistanceInMetersTo(oneDegreeEast);

        distance.ShouldBe(110_950, tolerance: 500);
    }

    [Fact]
    public void DistanceInMetersTo_TheSamePoint_IsZero()
    {
        var point = Coordinate.Create(4.7110, -74.0721).Value;

        point.DistanceInMetersTo(point).ShouldBe(0);
    }

    [Fact]
    public void DistanceInMetersTo_IsSymmetric()
    {
        var a = Coordinate.Create(4.7110, -74.0721).Value;
        var b = Coordinate.Create(4.7150, -74.0700).Value;

        a.DistanceInMetersTo(b).ShouldBe(b.DistanceInMetersTo(a), tolerance: 0.0001);
    }

    [Fact]
    public void ToCacheKey_ForCoordinatesWithinGpsNoise_ProducesTheSameKey()
    {
        // Dos lecturas de un vehículo parado que difieren en la séptima cifra decimal: unos
        // centímetros. Deben colapsar a la misma clave o la deduplicación no atraparía nada.
        var first = Coordinate.Create(4.7110001, -74.0721001).Value;
        var second = Coordinate.Create(4.7110002, -74.0721002).Value;

        first.ToCacheKey().ShouldBe(second.ToCacheKey());
    }

    [Fact]
    public void ToCacheKey_UsesInvariantFormatting()
    {
        // El contenedor podría arrancar con una cultura que usa coma decimal; la clave de Redis
        // cambiaría y la deduplicación fallaría solo en ese despliegue.
        var coordinate = Coordinate.Create(4.5, -74.25).Value;

        coordinate.ToCacheKey().ShouldBe("4.5:-74.25");
    }
}
