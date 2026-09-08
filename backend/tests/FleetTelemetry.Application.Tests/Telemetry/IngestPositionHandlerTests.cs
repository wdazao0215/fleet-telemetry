using FleetTelemetry.Application.Abstractions.Ports;
using FleetTelemetry.Application.Configuration;
using FleetTelemetry.Application.Telemetry.IngestPosition;
using FleetTelemetry.Application.Tests.Doubles;
using FleetTelemetry.Contracts;
using FleetTelemetry.Contracts.Events;
using FleetTelemetry.Domain.Telemetry;
using NSubstitute;
using Shouldly;

namespace FleetTelemetry.Application.Tests.Telemetry;

public class IngestPositionHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);

    private readonly IPositionCache cache = Substitute.For<IPositionCache>();
    private readonly IEventBus eventBus = Substitute.For<IEventBus>();
    private readonly TelemetryOptions options = new();

    [Fact]
    public async Task HandleAsync_WithANewReading_PublishesItForProcessing()
    {
        GivenTheReadingIsNew();
        var handler = BuildHandler();

        var result = await handler.HandleAsync(ValidCommand(), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Duplicate.ShouldBeFalse();

        await eventBus.Received(1).PublishAsync(
            Arg.Is<PositionAcceptedEvent>(e => e.VehicleId == "VH-001" && e.Latitude == 4.7110),
            Topology.RoutingKeys.PositionAccepted,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_WithADuplicateReading_DoesNotPublishIt()
    {
        // El simulador envía un 10% de duplicados por diseño. Un duplicado no es un error: es un
        // resultado normal que simplemente no debe llegar al worker.
        GivenTheReadingIsADuplicate();
        var handler = BuildHandler();

        var result = await handler.HandleAsync(ValidCommand(), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Duplicate.ShouldBeTrue();

        await eventBus.DidNotReceiveWithAnyArgs()
            .PublishAsync(default(PositionAcceptedEvent)!, default!, default);
    }

    [Fact]
    public async Task HandleAsync_WithAnInvalidPayload_FailsWithoutTouchingTheCacheOrTheBus()
    {
        // Un payload malformado no debe gastar una llamada a Redis ni ocupar sitio en la cola: se
        // rechaza en el borde. Con un 5% de tráfico malformado, ese ahorro es medible.
        var handler = BuildHandler();

        var result = await handler.HandleAsync(ValidCommand() with { Latitude = 999 }, CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(TelemetryErrors.LatitudeOutOfRange);

        await cache.DidNotReceiveWithAnyArgs().TryRegisterUniqueReadingAsync(default!, default, default);
        await eventBus.DidNotReceiveWithAnyArgs()
            .PublishAsync(default(PositionAcceptedEvent)!, default!, default);
    }

    [Fact]
    public async Task HandleAsync_WithAFutureTimestamp_IsRejected()
    {
        var handler = BuildHandler();

        var result = await handler.HandleAsync(
            ValidCommand() with { RecordedAt = Now.AddHours(1) }, CancellationToken.None);

        result.Error.ShouldBe(TelemetryErrors.TimestampInFuture);
    }

    [Fact]
    public async Task HandleAsync_PropagatesTheCorrelationIdIntoTheEvent()
    {
        // Es lo que permite seguir una posición desde el POST hasta el dashboard atravesando el
        // broker y los tres servicios. Sin él, depurar el flujo distribuido es adivinar.
        GivenTheReadingIsNew();
        var handler = BuildHandler();

        await handler.HandleAsync(ValidCommand() with { CorrelationId = "trace-42" }, CancellationToken.None);

        await eventBus.Received(1).PublishAsync(
            Arg.Is<PositionAcceptedEvent>(e => e.CorrelationId == "trace-42"),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_UsesTheConfiguredDeduplicationWindow()
    {
        GivenTheReadingIsNew();
        options.DeduplicationWindowSeconds = 30;
        var handler = BuildHandler();

        await handler.HandleAsync(ValidCommand(), CancellationToken.None);

        await cache.Received(1).TryRegisterUniqueReadingAsync(
            Arg.Any<TelemetryReading>(),
            TimeSpan.FromSeconds(30),
            Arg.Any<CancellationToken>());
    }

    private IngestPositionHandler BuildHandler() =>
        new(cache, eventBus, new FixedClock(Now), options);

    private static IngestPositionCommand ValidCommand() =>
        new("VH-001", 4.7110, -74.0721, Now.AddSeconds(-1), "correlation");

    private void GivenTheReadingIsNew() => cache
        .TryRegisterUniqueReadingAsync(Arg.Any<TelemetryReading>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
        .Returns(true);

    private void GivenTheReadingIsADuplicate() => cache
        .TryRegisterUniqueReadingAsync(Arg.Any<TelemetryReading>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
        .Returns(false);
}
