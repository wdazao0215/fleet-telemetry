namespace FleetTelemetry.Simulator;

/// <summary>
/// Vehículo simulado que recorre una ruta por Bogotá.
/// </summary>
/// <remarks>
/// El movimiento es un rumbo con desviaciones aleatorias suaves en lugar de saltos: una ruta a
/// tirones daría distancias enormes entre lecturas consecutivas y no ejercitaría la lógica de
/// detección, que compara posiciones próximas.
/// </remarks>
internal sealed class SimulatedVehicle
{
    private const double MetersPerDegreeLatitude = 111_320.0;

    private readonly Random random;
    private readonly bool isStationary;
    private double headingRadians;

    public SimulatedVehicle(string id, double latitude, double longitude, bool isStationary, Random random)
    {
        Id = id;
        Latitude = latitude;
        Longitude = longitude;
        this.isStationary = isStationary;
        this.random = random;
        headingRadians = random.NextDouble() * 2 * Math.PI;
    }

    public string Id { get; }

    public double Latitude { get; private set; }

    public double Longitude { get; private set; }

    /// <summary>Avanza la posición hasta la siguiente lectura.</summary>
    public void Advance(TimeSpan elapsed)
    {
        if (isStationary)
        {
            // Deriva de ±1.5 m: es el ruido real de un GPS civil en reposo. Mantenerlo hace que el
            // detector tenga que trabajar de verdad; con una coordenada exacta repetida, la
            // detección sería trivial y no probaría nada.
            Latitude += (random.NextDouble() - 0.5) * 0.00003;
            Longitude += (random.NextDouble() - 0.5) * 0.00003;
            return;
        }

        // Entre 20 y 60 km/h, valores urbanos plausibles.
        var speedMetersPerSecond = 5.5 + (random.NextDouble() * 11.0);
        var distance = speedMetersPerSecond * elapsed.TotalSeconds;

        headingRadians += (random.NextDouble() - 0.5) * 0.4;

        var deltaLatitude = distance * Math.Cos(headingRadians) / MetersPerDegreeLatitude;
        var deltaLongitude = distance * Math.Sin(headingRadians) /
                             (MetersPerDegreeLatitude * Math.Cos(Latitude * Math.PI / 180.0));

        Latitude += deltaLatitude;
        Longitude += deltaLongitude;

        // Rebote dentro del área urbana de Bogotá: sin esto, tras una hora los vehículos estarían
        // en mitad del Atlántico y el mapa no mostraría nada útil.
        if (Latitude is < 4.55 or > 4.80 || Longitude is < -74.20 or > -74.00)
        {
            headingRadians += Math.PI;
        }
    }
}
