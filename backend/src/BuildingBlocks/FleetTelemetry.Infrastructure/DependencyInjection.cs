using FleetTelemetry.Application.Abstractions.Messaging;
using FleetTelemetry.Application.Abstractions.Ports;
using FleetTelemetry.Application.Configuration;
using FleetTelemetry.Application.Alerts.ProcessPanic;
using FleetTelemetry.Application.Alerts.RaisePanic;
using FleetTelemetry.Application.Telemetry.IngestPosition;
using FleetTelemetry.Application.Telemetry.ProcessPosition;
using FleetTelemetry.Infrastructure.Persistence;
using FleetTelemetry.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using FleetTelemetry.Infrastructure.Caching;
using FleetTelemetry.Infrastructure.Cqrs;
using FleetTelemetry.Infrastructure.Messaging;
using FleetTelemetry.Infrastructure.Time;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace FleetTelemetry.Infrastructure;

/// <summary>
/// Composición de la infraestructura. Es el único sitio donde se decide qué adaptador implementa
/// cada puerto.
/// </summary>
public static class DependencyInjection
{
    /// <summary>Registra opciones, reloj y CQRS: lo que necesita cualquiera de los servicios.</summary>
    public static IServiceCollection AddTelemetryCore(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<TelemetryOptions>(configuration.GetSection(TelemetryOptions.SectionName));
        services.AddSingleton(sp => sp.GetRequiredService<IOptions<TelemetryOptions>>().Value);

        services.AddSingleton<IClock, SystemClock>();

        services.AddScoped<ICommandDispatcher, CommandDispatcher>();
        services.AddScoped<IQueryDispatcher, QueryDispatcher>();
        services.AddTransient(typeof(ICommandPipelineBehavior<,>), typeof(CommandLoggingBehavior<,>));

        return services;
    }

    public static IServiceCollection AddRedisCache(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<RedisOptions>(configuration.GetSection(RedisOptions.SectionName));

        services.AddSingleton<IConnectionMultiplexer>(provider =>
        {
            var options = provider.GetRequiredService<IOptions<RedisOptions>>().Value;
            var configurationOptions = ConfigurationOptions.Parse(options.ConnectionString);
            configurationOptions.AbortOnConnectFail = options.AbortOnConnectFail;
            configurationOptions.ConnectTimeout = options.ConnectTimeoutMilliseconds;
            return ConnectionMultiplexer.Connect(configurationOptions);
        });

        services.AddSingleton<IPositionCache, RedisPositionCache>();

        return services;
    }

    /// <summary>Publicador resiliente: reintentos, circuit breaker y buffer local.</summary>
    public static IServiceCollection AddResilientEventBus(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<RabbitMqOptions>(configuration.GetSection(RabbitMqOptions.SectionName));

        services.AddSingleton<RabbitMqConnectionProvider>();
        services.AddSingleton<RabbitMqEventBus>();
        services.AddSingleton<IMessagePublisher>(sp => sp.GetRequiredService<RabbitMqEventBus>());
        services.AddSingleton<ResilienceState>();
        services.AddSingleton<IFallbackBuffer, ChannelFallbackBuffer>();
        services.AddSingleton<IEventBus, ResilientEventBus>();

        services.AddHostedService<RabbitMqTopologyInitializer>();
        services.AddHostedService<FallbackDrainService>();

        return services;
    }

    /// <summary>Persistencia sobre PostgreSQL + TimescaleDB.</summary>
    public static IServiceCollection AddFleetPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("FleetDb")
            ?? throw new InvalidOperationException("Falta la cadena de conexión 'ConnectionStrings:FleetDb'.");

        services.AddDbContext<FleetDbContext>(options => options
            .UseNpgsql(connectionString, npgsql => npgsql.EnableRetryOnFailure(
                maxRetryCount: 3,
                maxRetryDelay: TimeSpan.FromSeconds(5),
                errorCodesToAdd: null)));

        services.AddScoped<IPositionRepository, TimescalePositionRepository>();
        services.AddScoped<IVehicleRepository, VehicleRepository>();
        services.AddScoped<IAlertRepository, AlertRepository>();

        return services;
    }

    /// <summary>Casos de uso del worker de procesamiento.</summary>
    public static IServiceCollection AddProcessingUseCases(this IServiceCollection services)
    {
        services.AddScoped<ICommandHandler<ProcessPositionCommand, ProcessPositionResult>, ProcessPositionHandler>();
        services.AddScoped<ICommandHandler<ProcessPanicCommand, ProcessPanicResult>, ProcessPanicHandler>();
        return services;
    }

    /// <summary>
    /// Casos de uso de la ingesta.
    /// </summary>
    /// <remarks>
    /// El registro es explícito y no por escaneo de ensamblados: cuesta una línea por caso de uso y
    /// a cambio el fallo aparece al compilar, no en runtime cuando falta un handler.
    /// </remarks>
    public static IServiceCollection AddIngestionUseCases(this IServiceCollection services)
    {
        services.AddScoped<ICommandHandler<IngestPositionCommand, IngestPositionResult>, IngestPositionHandler>();
        services.AddScoped<ICommandHandler<RaisePanicCommand, RaisePanicResult>, RaisePanicHandler>();
        return services;
    }
}
