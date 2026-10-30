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
        var types = Types.InAssembly(DomainAssembly).GetTypes().ToArray();
        types.ShouldNotBeEmpty("Domain assembly must contain types to test non-vacuously.");

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
        var types = Types.InAssembly(ServiceDefaultsAssembly).GetTypes().ToArray();
        types.ShouldNotBeEmpty("ServiceDefaults assembly must contain types to test non-vacuously.");

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

    [Fact(Skip = "Pending implementation of service domain projects (Inventory.Domain, Requests.Domain) in Phase 2.")]
    public void DomainProjects_ShouldNotReferenceEFCore_MassTransit_AspNetCore_OrServiceDefaults()
    {
        // Plan section 12, rules 2 & 3:
        // *.Domain projects reference no EF Core, no MassTransit, and no ASP.NET Core.
        // They may reference only BuildingBlocks.Domain and standard C# BCL.
        // *.Domain projects do not reference ServiceDefaults.
    }

    [Fact(Skip = "Pending implementation of service contracts projects (*.Contracts) in Phase 2.")]
    public void ContractsProjects_ShouldContainOnlyRecordsAndEnums_AndReferenceNothing()
    {
        // Plan section 12, rule 5:
        // *.Contracts projects contain only records and enums and reference nothing.
    }

    [Fact(Skip = "Pending implementation of service projects (Inventory, Requests, Dispatch) in Phase 2.")]
    public void Services_ShouldNeverReferenceOtherServicesApiOrDomain_OnlyContracts()
    {
        // Plan section 12, rule 1:
        // A service's *.Api and *.Domain projects never reference another service's *.Api or *.Domain.
        // They may reference only another service's *.Contracts.
    }

    [Fact(Skip = "Pending implementation of messaging infrastructure and sagas in Phase 4 & Phase 6.")]
    public void MassTransitTypes_ShouldOnlyBeReferencedByMessagingInfrastructureOrRequestsSagas()
    {
        // Plan section 12, rule 4:
        // Only Infrastructure/Messaging (and Requests.Api/Sagas) may reference MassTransit types.
    }
}

