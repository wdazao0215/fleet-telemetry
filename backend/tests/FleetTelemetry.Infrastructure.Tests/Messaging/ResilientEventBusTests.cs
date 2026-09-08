using FleetTelemetry.Contracts.Events;
using FleetTelemetry.Infrastructure.Messaging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Shouldly;

namespace FleetTelemetry.Infrastructure.Tests.Messaging;

/// <summary>
/// El requisito del enunciado: "si el servicio de ruteo está inaccesible, el sistema de ingesta no
/// debe caer (debe encolar o manejar el error elegantemente)".
/// </summary>
public class ResilientEventBusTests
{
    private static readonly RabbitMqOptions FastFailingOptions = new()
    {
        MaxRetryAttempts = 1,
        RetryBaseDelayMilliseconds = 1,
        CircuitMinimumThroughput = 2,
        CircuitSamplingDurationSeconds = 30,
        CircuitBreakDurationSeconds = 30,
        CircuitFailureRatio = 0.5,
        FallbackBufferCapacity = 100,
    };

    [Fact]
    public async Task PublishAsync_WhenTheBrokerIsUnreachable_DoesNotThrow()
    {
        var (bus, _, buffer) = Build(publisherFails: true);

        // Sin aserción de excepción explícita: si esto lanzara, la ingesta devolvería 500 y el
        // vehículo perdería la lectura. El test falla por la excepción misma.
        await bus.PublishAsync(AnyEvent(), "position.accepted", CancellationToken.None);

        buffer.Count.ShouldBe(1);
    }

    [Fact]
    public async Task PublishAsync_WhenTheBrokerIsUnreachable_BuffersTheMessageInsteadOfLosingIt()
    {
        var (bus, publisher, buffer) = Build(publisherFails: true);

        await bus.PublishAsync(AnyEvent(), "position.accepted", CancellationToken.None);

        buffer.Count.ShouldBe(1);
        await publisher.ReceivedWithAnyArgs().PublishRawAsync(default!, default!, default!, default);
    }

    [Fact]
    public async Task PublishAsync_AfterEnoughFailures_OpensTheCircuit()
    {
        var (bus, publisher, _, state) = BuildWithState(publisherFails: true);

        for (var attempt = 0; attempt < 10; attempt++)
        {
            await bus.PublishAsync(AnyEvent(), "position.accepted", CancellationToken.None);
        }

        state.CircuitState.ShouldBe("Open");
        state.IsHealthy.ShouldBeFalse();

        // Con el circuito abierto las llamadas fallan de inmediato y ya no golpean al broker. Es lo
        // que evita que la ingesta muera esperando timeouts aunque técnicamente "siga en pie".
        publisher.ClearReceivedCalls();
        await bus.PublishAsync(AnyEvent(), "position.accepted", CancellationToken.None);
        await publisher.DidNotReceiveWithAnyArgs().PublishRawAsync(default!, default!, default!, default);
    }

    [Fact]
    public async Task PublishAsync_WhenTheBrokerIsHealthy_PublishesWithoutBuffering()
    {
        var (bus, publisher, buffer) = Build(publisherFails: false);

        await bus.PublishAsync(AnyEvent(), "position.accepted", CancellationToken.None);

        buffer.Count.ShouldBe(0);
        await publisher.ReceivedWithAnyArgs(1).PublishRawAsync(default!, default!, default!, default);
    }

    [Fact]
    public async Task PublishAsync_WhenTheBufferIsFull_DropsTheOldestAndKeepsAccepting()
    {
        // Un buffer ilimitado convertiría una caída del broker en una caída por memoria. Se prefiere
        // perder las posiciones más antiguas: un vehículo se localiza con la última, no con la de
        // hace veinte minutos.
        var options = new RabbitMqOptions
        {
            MaxRetryAttempts = 1,
            RetryBaseDelayMilliseconds = 1,
            CircuitMinimumThroughput = 1000,
            FallbackBufferCapacity = 3,
        };

        var (bus, _, buffer) = Build(publisherFails: true, options);

        for (var attempt = 0; attempt < 10; attempt++)
        {
            await bus.PublishAsync(AnyEvent(), "position.accepted", CancellationToken.None);
        }

        buffer.Count.ShouldBe(3);
        buffer.DroppedCount.ShouldBe(7);
    }

    private static PositionAcceptedEvent AnyEvent() => new(
        Guid.CreateVersion7(), DateTimeOffset.UtcNow, "correlation", "VH-001", 4.7110, -74.0721,
        DateTimeOffset.UtcNow);

    private static (ResilientEventBus Bus, IMessagePublisher Publisher, IFallbackBuffer Buffer) Build(
        bool publisherFails,
        RabbitMqOptions? options = null)
    {
        var (bus, publisher, buffer, _) = BuildWithState(publisherFails, options);
        return (bus, publisher, buffer);
    }

    private static (ResilientEventBus Bus, IMessagePublisher Publisher, IFallbackBuffer Buffer, ResilienceState State)
        BuildWithState(bool publisherFails, RabbitMqOptions? options = null)
    {
        var settings = Options.Create(options ?? FastFailingOptions);
        var publisher = Substitute.For<IMessagePublisher>();

        if (publisherFails)
        {
            publisher
                .PublishRawAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
                .ThrowsAsync(new InvalidOperationException("broker unreachable"));
        }

        var buffer = new ChannelFallbackBuffer(settings, NullLogger<ChannelFallbackBuffer>.Instance);
        var state = new ResilienceState();
        var bus = new ResilientEventBus(publisher, buffer, state, settings, NullLogger<ResilientEventBus>.Instance);

        return (bus, publisher, buffer, state);
    }
}
