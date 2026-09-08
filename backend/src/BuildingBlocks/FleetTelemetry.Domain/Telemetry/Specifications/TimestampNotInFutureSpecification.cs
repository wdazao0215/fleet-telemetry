using FleetTelemetry.Domain.Common;

namespace FleetTelemetry.Domain.Telemetry.Specifications;

public sealed class TimestampNotInFutureSpecification(DateTimeOffset now, TimeSpan clockSkewTolerance)
    : ISpecification<TelemetryReading>
{
    public Error Error => TelemetryErrors.TimestampInFuture;

    public bool IsSatisfiedBy(TelemetryReading candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        return candidate.RecordedAt <= now + clockSkewTolerance;
    }
}
