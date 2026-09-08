using System.Reflection;
using FleetTelemetry.Domain.Common;
using NetArchTest.Rules;
using Shouldly;

namespace FleetTelemetry.Architecture.Tests;

public class DomainConventionTests
{
    private static readonly Assembly DomainAssembly = typeof(Result).Assembly;

    [Fact]
    public void DomainEntities_AreSealed()
    {
        // Sellar por defecto: la herencia en entidades de dominio suele acabar siendo una jerarquía
        // que nadie recuerda por qué existe. Si hiciera falta polimorfismo, se abre a conciencia.
        var result = Types.InAssembly(DomainAssembly)
            .That()
            .AreClasses()
            .And()
            .AreNotAbstract()
            .And()
            .DoNotHaveName(nameof(Result))
            .Should()
            .BeSealed()
            .GetResult();

        result.FailingTypeNames.ShouldBeNull();
    }

    [Fact]
    public void Specifications_LiveInTheSpecificationsNamespace()
    {
        var result = Types.InAssembly(DomainAssembly)
            .That()
            .ImplementInterface(typeof(ISpecification<>))
            .Should()
            .ResideInNamespaceContaining("Specifications")
            .GetResult();

        result.FailingTypeNames.ShouldBeNull();
    }
}
