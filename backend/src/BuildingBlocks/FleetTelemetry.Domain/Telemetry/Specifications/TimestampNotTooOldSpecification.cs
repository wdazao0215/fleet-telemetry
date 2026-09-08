using FleetTelemetry.Domain.Common;

namespace FleetTelemetry.Domain.Telemetry.Specifications;

public sealed class TimestampNotTooOldSpecification(DateTimeOffset now, TimeSpan maximumAge)
    : ISpecification<TelemetryReading>
{
    public Error Error => TelemetryErrors.TimestampTooOld;

    public bool IsSatisfiedBy(TelemetryReading candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        return candidate.RecordedAt >= now - maximumAge;
    }
}
