using System.Reflection;
using CryptoIntelligence.Application.Configuration;
using CryptoIntelligence.Contracts;
using CryptoIntelligence.Domain.Common;
using CryptoIntelligence.Infrastructure.Persistence;

namespace CryptoIntelligence.ArchitectureTests;

public sealed class DependencyRuleTests
{
    [Fact]
    public void Domain_does_not_reference_other_solution_layers()
    {
        AssertDoesNotReference(
            typeof(Slot).Assembly,
            "CryptoIntelligence.Application",
            "CryptoIntelligence.Infrastructure",
            "CryptoIntelligence.Api",
            "CryptoIntelligence.Worker",
            "CryptoIntelligence.Contracts");
    }

    [Fact]
    public void Application_does_not_reference_infrastructure_or_hosts()
    {
        AssertDoesNotReference(
            typeof(MvpConfiguration).Assembly,
            "CryptoIntelligence.Infrastructure",
            "CryptoIntelligence.Api",
            "CryptoIntelligence.Worker");
    }

    [Fact]
    public void Contracts_remain_transport_only()
    {
        AssertDoesNotReference(
            typeof(SystemStatusResponse).Assembly,
            "CryptoIntelligence.Domain",
            "CryptoIntelligence.Application",
            "CryptoIntelligence.Infrastructure",
            "CryptoIntelligence.Api",
            "CryptoIntelligence.Worker");
    }

    [Fact]
    public void Infrastructure_does_not_reference_hosts()
    {
        AssertDoesNotReference(
            typeof(CryptoIntelligenceDbContext).Assembly,
            "CryptoIntelligence.Api",
            "CryptoIntelligence.Worker");
    }

    private static void AssertDoesNotReference(Assembly assembly, params string[] forbiddenNames)
    {
        var references = assembly
            .GetReferencedAssemblies()
            .Select(reference => reference.Name)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var forbiddenName in forbiddenNames)
        {
            Assert.DoesNotContain(forbiddenName, references);
        }
    }
}
