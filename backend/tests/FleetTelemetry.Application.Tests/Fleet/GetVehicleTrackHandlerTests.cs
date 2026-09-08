using FleetTelemetry.Application.Abstractions.Models;
using FleetTelemetry.Application.Abstractions.Ports;
using FleetTelemetry.Application.Fleet.GetVehicleTrack;
using FleetTelemetry.Domain.Telemetry;
using FleetTelemetry.Domain.Vehicles;
using NSubstitute;
using Shouldly;

namespace FleetTelemetry.Application.Tests.Fleet;

public class GetVehicleTrackHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);

    private readonly IPositionRepository positions = Substitute.For<IPositionRepository>();

    [Fact]
    public async Task HandleAsync_WithAnInvertedRange_IsRejected()
    {
        var result = await new GetVehicleTrackHandler(positions).HandleAsync(
            new GetVehicleTrackQuery("VH-001", Now, Now.AddHours(-1)), CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("track.invalid_range");
    }

    [Fact]
    public async Task HandleAsync_WithAnInvalidVehicleId_IsRejected()
    {
        var result = await new GetVehicleTrackHandler(positions).HandleAsync(
            new GetVehicleTrackQuery("VH:001", Now.AddHours(-1), Now), CancellationToken.None);

        result.Error.ShouldBe(TelemetryErrors.VehicleIdInvalidFormat);
    }

    [Fact]
    public async Task HandleAsync_ClampsAnExcessivePointLimit()
    {
        // Un cliente que pida 100.000 puntos congelaría su propio navegador dibujando la polilínea.
        // El tope se aplica en el servidor, no se confía en el cliente.
        GivenNoPoints();

        await new GetVehicleTrackHandler(positions).HandleAsync(
            new GetVehicleTrackQuery("VH-001", Now.AddHours(-1), Now, MaxPoints: 100_000),
            CancellationToken.None);

        await positions.Received(1).GetTrackAsync(
            Arg.Any<VehicleId>(), Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>(), 2_000,
            Arg.Any<CancellationToken>());
    }

    private void GivenNoPoints() => positions
        .GetTrackAsync(
            Arg.Any<VehicleId>(), Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>(), Arg.Any<int>(),
            Arg.Any<CancellationToken>())
        .Returns(Array.Empty<TrackPoint>());
}
