using FleetTelemetry.Application.Abstractions.Ports;

namespace FleetTelemetry.Application.Tests.Doubles;

/// <summary>Reloj congelado: hace deterministas las reglas que dependen del tiempo.</summary>
internal sealed class FixedClock(DateTimeOffset now) : IClock
{
    public DateTimeOffset UtcNow { get; set; } = now;

    public void Advance(TimeSpan amount) => UtcNow += amount;
}
