using BloodLink.BuildingBlocks.Domain;
using BloodLink.Inventory.Contracts;
using BloodLink.Inventory.Domain.Model.BloodUnits;
using BloodLink.ServiceDefaults;
using NetArchTest.Rules;
using Shouldly;
using Xunit;

namespace BloodLink.Architecture.Tests;

/// <summary>
/// Architecture boundary and isolation tests enforcing BloodLink foundational architectural rules.
///
/// Package Decision Note:
/// NetArchTest.eNhancedEdition 1.4.5 is intentionally used rather than the legacy NetArchTest 1.3.2.
/// The original NetArchTest library has been unmaintained since 2020 and does not properly recognize
/// modern C# features (e.g., records, init properties, nullable references) on modern .NET SDKs.
/// NetArchTest.eNhancedEdition is the drop-in, actively maintained fork supporting modern .NET language constructs.
/// </summary>
public class FoundationArchitectureTests
{
    private static readonly System.Reflection.Assembly BuildingBlocksDomainAssembly =
        typeof(IDomainEvent).Assembly;
    private static readonly System.Reflection.Assembly ServiceDefaultsAssembly =
        typeof(Extensions).Assembly;
    private static readonly System.Reflection.Assembly InventoryDomainAssembly =
        typeof(BloodUnit).Assembly;
    private static readonly System.Reflection.Assembly InventoryContractsAssembly =
        typeof(UnitReceivedIntegrationEvent).Assembly;

    [Fact]
    public void BuildingBlocksDomain_ShouldNotHaveDependencyOnInfrastructureFrameworks()
    {
        Type[] types = BuildingBlocksDomainAssembly.GetExportedTypes();
        types.ShouldNotBeEmpty(
            "BuildingBlocks.Domain assembly must contain types to test non-vacuously."
        );

        string[] forbiddenDependencies =
        [
            "Microsoft.EntityFrameworkCore",
            "MassTransit",
            "Microsoft.AspNetCore",
            "BloodLink.ServiceDefaults",
        ];

        TestResult result = Types
            .InAssembly(BuildingBlocksDomainAssembly)
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
        Type[] types = ServiceDefaultsAssembly.GetExportedTypes();
        types.ShouldNotBeEmpty(
            "ServiceDefaults assembly must contain types to test non-vacuously."
        );

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

    [Fact]
    public void DomainProjects_ShouldNotReferenceEFCore_MassTransit_AspNetCore_OrServiceDefaults()
    {
        // Boundary rules:
        // *.Domain projects reference no EF Core, no MassTransit, and no ASP.NET Core.
        // They may reference only BuildingBlocks.Domain and standard C# BCL.
        // *.Domain projects do not reference ServiceDefaults.
        Type[] types = InventoryDomainAssembly.GetExportedTypes();
        types.ShouldNotBeEmpty(
            "Inventory.Domain assembly must contain types to test non-vacuously."
        );

        string[] forbiddenDependencies =
        [
            "Microsoft.EntityFrameworkCore",
            "MassTransit",
            "Microsoft.AspNetCore",
            "BloodLink.ServiceDefaults",
        ];

        TestResult result = Types
            .InAssembly(InventoryDomainAssembly)
            .ShouldNot()
            .HaveDependencyOnAny(forbiddenDependencies)
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(
            $"Inventory.Domain must not depend on external frameworks or ServiceDefaults: {string.Join(", ", result.FailingTypes?.Select(t => t.FullName) ?? [])}"
        );

        // Also assert that references are limited to BuildingBlocks.Domain and BCL
        var referencedAssemblies = InventoryDomainAssembly
            .GetReferencedAssemblies()
            .Select(a => a.Name)
            .Where(name => name != null)
            .ToArray();

        foreach (var refName in referencedAssemblies)
        {
            bool isAllowed =
                refName!.StartsWith("System", StringComparison.Ordinal)
                || refName.StartsWith("Microsoft.NET", StringComparison.Ordinal)
                || refName.Equals("mscorlib", StringComparison.Ordinal)
                || refName.Equals("BloodLink.BuildingBlocks.Domain", StringComparison.Ordinal);

            isAllowed.ShouldBeTrue(
                $"Inventory.Domain must only reference BuildingBlocks.Domain and BCL, but references '{refName}'."
            );
        }
    }

    [Fact]
    public void ContractsProjects_ShouldContainOnlyRecordsAndEnums_AndReferenceNothing()
    {
        // Boundary rule:
        // *.Contracts projects contain only records and enums and reference nothing outside BCL.
        Type[] types = InventoryContractsAssembly.GetExportedTypes();
        types.ShouldNotBeEmpty("Inventory.Contracts must contain types to test non-vacuously.");

        foreach (Type type in types)
        {
            bool isEnum = type.IsEnum;
            bool isRecord =
                type.GetMethods().Any(m => m.Name == "<Clone>$")
                || type.GetProperty(
                    "EqualityContract",
                    System.Reflection.BindingFlags.NonPublic
                        | System.Reflection.BindingFlags.Instance
                ) != null;

            (isEnum || isRecord).ShouldBeTrue(
                $"Contracts type {type.FullName} must be a record or an enum."
            );
        }

        string[] forbiddenDependencies =
        [
            "Microsoft.EntityFrameworkCore",
            "MassTransit",
            "Microsoft.AspNetCore",
            "BloodLink.BuildingBlocks.Domain",
            "BloodLink.ServiceDefaults",
            "FluentValidation",
            "Riok.Mapperly",
        ];

        TestResult result = Types
            .InAssembly(InventoryContractsAssembly)
            .ShouldNot()
            .HaveDependencyOnAny(forbiddenDependencies)
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(
            $"Inventory.Contracts must not depend on external libraries: {string.Join(", ", result.FailingTypes?.Select(t => t.FullName) ?? [])}"
        );

        var referencedAssemblies = InventoryContractsAssembly
            .GetReferencedAssemblies()
            .Select(a => a.Name)
            .Where(name => name != null)
            .ToArray();

        foreach (var refName in referencedAssemblies)
        {
            bool isBcl =
                refName!.StartsWith("System", StringComparison.Ordinal)
                || refName.StartsWith("Microsoft.NET", StringComparison.Ordinal)
                || refName.Equals("mscorlib", StringComparison.Ordinal);

            isBcl.ShouldBeTrue(
                $"Inventory.Contracts must reference only BCL assemblies, but references '{refName}'."
            );
        }
    }

    [Fact(
        Skip = "Skipped in Phase 2: requires a second service (Requests.Api / Requests.Domain) to test cross-service boundary enforcement in Phase 5."
    )]
    public void Services_ShouldNeverReferenceOtherServicesApiOrDomain_OnlyContracts()
    {
        // Boundary rule:
        // A service's *.Api and *.Domain projects never reference another service's *.Api or *.Domain.
        // They may reference only another service's *.Contracts.
    }

    [Fact(
        Skip = "Skipped in Phase 2: requires messaging infrastructure and sagas implementation in Phase 4 & Phase 6."
    )]
    public void MassTransitTypes_ShouldOnlyBeReferencedByMessagingInfrastructureOrRequestsSagas()
    {
        // Boundary rule:
        // Only Infrastructure/Messaging (and Requests.Api/Sagas) may reference MassTransit types.
    }
}
