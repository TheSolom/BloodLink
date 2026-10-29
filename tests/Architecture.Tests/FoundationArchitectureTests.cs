using BloodLink.BuildingBlocks.Domain;
using BloodLink.ServiceDefaults;
using NetArchTest.Rules;
using Shouldly;
using Xunit;

namespace BloodLink.Architecture.Tests;

public class FoundationArchitectureTests
{
    private static readonly System.Reflection.Assembly DomainAssembly =
        typeof(IDomainEvent).Assembly;
    private static readonly System.Reflection.Assembly ServiceDefaultsAssembly =
        typeof(Extensions).Assembly;

    [Fact]
    public void BuildingBlocksDomain_ShouldNotHaveDependencyOnInfrastructureFrameworks()
    {
        // Domain building blocks must stay purely in C# standard library with no EF, ASP.NET Core, or MassTransit
        string[] forbiddenDependencies =
        [
            "Microsoft.EntityFrameworkCore",
            "MassTransit",
            "Microsoft.AspNetCore",
            "BloodLink.ServiceDefaults",
        ];

        TestResult result = Types
            .InAssembly(DomainAssembly)
            .ShouldNot()
            .HaveDependencyOnAny(forbiddenDependencies)
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(
            $"BuildingBlocks.Domain must not depend on external frameworks: {string.Join(", ", result.FailingTypes?.Select(t => t.FullName) ?? [])}"
        );
    }

    [Fact]
    public void ServiceDefaults_ShouldNotDependOnDomainBuildingBlocks()
    {
        // ServiceDefaults handles cross-cutting operational concerns (OTEL, Health checks)
        // and must never know about domain models or domain events.
        string[] forbiddenDependencies = ["BloodLink.BuildingBlocks.Domain"];

        TestResult result = Types
            .InAssembly(ServiceDefaultsAssembly)
            .ShouldNot()
            .HaveDependencyOnAny(forbiddenDependencies)
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(
            $"ServiceDefaults must not depend on Domain: {string.Join(", ", result.FailingTypes?.Select(t => t.FullName) ?? [])}"
        );
    }
}

