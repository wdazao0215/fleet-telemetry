using System.Reflection;
using FleetTelemetry.Application.Abstractions.Messaging;
using FleetTelemetry.Domain.Common;
using NetArchTest.Rules;
using Shouldly;

namespace FleetTelemetry.Architecture.Tests;

/// <summary>
/// La regla de capas descrita en CLAUDE.md, hecha ejecutable.
/// </summary>
/// <remarks>
/// Una convención documentada se erosiona en cuanto alguien tiene prisa: basta un
/// <c>using Microsoft.EntityFrameworkCore</c> en un handler para que Clean Architecture pase a ser
/// un diagrama sin correspondencia con el código. Aquí el build falla en su lugar.
/// </remarks>
public class LayerDependencyTests
{
    private static readonly Assembly DomainAssembly = typeof(Result).Assembly;
    private static readonly Assembly ApplicationAssembly = typeof(ICommandDispatcher).Assembly;

    private static readonly string[] InfrastructureNamespaces =
    [
        "Microsoft.EntityFrameworkCore",
        "Npgsql",
        "StackExchange.Redis",
        "RabbitMQ",
        "Polly",
        "Microsoft.AspNetCore",
        "Serilog",
        "FleetTelemetry.Infrastructure",
    ];

    [Fact]
    public void Domain_DoesNotDependOnAnyOtherLayer()
    {
        var result = Types.InAssembly(DomainAssembly)
            .ShouldNot()
            .HaveDependencyOnAny("FleetTelemetry.Application", "FleetTelemetry.Infrastructure", "FleetTelemetry.Contracts")
            .GetResult();

        result.FailingTypeNames.ShouldBeNull();
    }

    [Fact]
    public void Domain_DoesNotDependOnAnyFramework()
    {
        // El dominio debe poder testearse sin base de datos, sin contenedor de DI y sin host.
        var result = Types.InAssembly(DomainAssembly)
            .ShouldNot()
            .HaveDependencyOnAny(InfrastructureNamespaces)
            .GetResult();

        result.FailingTypeNames.ShouldBeNull();
    }

    [Fact]
    public void Application_DoesNotDependOnInfrastructure()
    {
        var result = Types.InAssembly(ApplicationAssembly)
            .ShouldNot()
            .HaveDependencyOnAny(InfrastructureNamespaces)
            .GetResult();

        result.FailingTypeNames.ShouldBeNull();
    }

    [Fact]
    public void Ports_AreDeclaredInApplicationAndNeverImplementedThere()
    {
        // Los puertos son interfaces por definición. Una implementación concreta dentro de
        // Application sería un adaptador infiltrado en la capa equivocada.
        var implementations = Types.InAssembly(ApplicationAssembly)
            .That()
            .ResideInNamespace("FleetTelemetry.Application.Abstractions.Ports")
            .And()
            .AreClasses()
            .GetTypes();

        implementations.ShouldBeEmpty();
    }
}
