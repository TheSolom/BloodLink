using BloodLink.BuildingBlocks.Domain;
using BloodLink.Inventory.Api.Infrastructure.Persistence;
using BloodLink.Inventory.Contracts;
using BloodLink.Inventory.Domain.Model.BloodUnits;
using BloodLink.Requests.Api.Infrastructure.Persistence;
using BloodLink.Requests.Contracts;
using BloodLink.Requests.Domain.BloodRequests;
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
    private static readonly System.Reflection.Assembly InventoryApiAssembly =
        typeof(InventoryDbContext).Assembly;
    private static readonly System.Reflection.Assembly RequestsDomainAssembly =
        typeof(BloodRequest).Assembly;
    private static readonly System.Reflection.Assembly RequestsContractsAssembly =
        typeof(BloodRequestStatusChangedIntegrationEvent).Assembly;
    private static readonly System.Reflection.Assembly RequestsApiAssembly =
        typeof(RequestsDbContext).Assembly;

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
        System.Reflection.Assembly[] domainAssemblies =
        [
            InventoryDomainAssembly,
            RequestsDomainAssembly,
        ];

        string[] forbiddenDependencies =
        [
            "Microsoft.EntityFrameworkCore",
            "MassTransit",
            "Microsoft.AspNetCore",
            "BloodLink.ServiceDefaults",
        ];

        foreach (System.Reflection.Assembly assembly in domainAssemblies)
        {
            Type[] types = assembly.GetExportedTypes();
            types.ShouldNotBeEmpty(
                $"{assembly.GetName().Name} assembly must contain types to test non-vacuously."
            );

            TestResult result = Types
                .InAssembly(assembly)
                .ShouldNot()
                .HaveDependencyOnAny(forbiddenDependencies)
                .GetResult();

            result.IsSuccessful.ShouldBeTrue(
                $"{assembly.GetName().Name} must not depend on external frameworks or ServiceDefaults: {string.Join(", ", result.FailingTypes?.Select(t => t.FullName) ?? [])}"
            );

            // Also assert that references are limited to BuildingBlocks.Domain and BCL
            string?[] referencedAssemblies = assembly
                .GetReferencedAssemblies()
                .Select(a => a.Name)
                .Where(name => name != null)
                .ToArray();

            foreach (string? refName in referencedAssemblies)
            {
                bool isAllowed =
                    refName!.StartsWith("System", StringComparison.Ordinal)
                    || refName.StartsWith("Microsoft.NET", StringComparison.Ordinal)
                    || refName.Equals("mscorlib", StringComparison.Ordinal)
                    || refName.Equals("netstandard", StringComparison.Ordinal)
                    || refName.Equals("BloodLink.BuildingBlocks.Domain", StringComparison.Ordinal);

                isAllowed.ShouldBeTrue(
                    $"{assembly.GetName().Name} must only reference BuildingBlocks.Domain and BCL, but references '{refName}'."
                );
            }
        }
    }

    [Fact]
    public void ContractsProjects_ShouldContainOnlyRecordsAndEnums_AndReferenceNothing()
    {
        // Boundary rule:
        // *.Contracts projects contain only records and enums and reference nothing outside BCL.
        System.Reflection.Assembly[] contractsAssemblies =
        [
            InventoryContractsAssembly,
            RequestsContractsAssembly,
        ];

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

        foreach (System.Reflection.Assembly assembly in contractsAssemblies)
        {
            Type[] types = assembly.GetExportedTypes();
            types.ShouldNotBeEmpty(
                $"{assembly.GetName().Name} must contain types to test non-vacuously."
            );

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
                    $"Contracts type {type.FullName} in {assembly.GetName().Name} must be a record or an enum."
                );
            }

            TestResult result = Types
                .InAssembly(assembly)
                .ShouldNot()
                .HaveDependencyOnAny(forbiddenDependencies)
                .GetResult();

            result.IsSuccessful.ShouldBeTrue(
                $"{assembly.GetName().Name} must not depend on external libraries: {string.Join(", ", result.FailingTypes?.Select(t => t.FullName) ?? [])}"
            );

            string?[] referencedAssemblies = assembly
                .GetReferencedAssemblies()
                .Select(a => a.Name)
                .Where(name => name != null)
                .ToArray();

            foreach (string? refName in referencedAssemblies)
            {
                bool isBcl =
                    refName!.StartsWith("System", StringComparison.Ordinal)
                    || refName.StartsWith("Microsoft.NET", StringComparison.Ordinal)
                    || refName.Equals("mscorlib", StringComparison.Ordinal)
                    || refName.Equals("netstandard", StringComparison.Ordinal);

                isBcl.ShouldBeTrue(
                    $"{assembly.GetName().Name} must reference only BCL assemblies, but references '{refName}'."
                );
            }
        }
    }

    [Fact]
    public void Services_ShouldNeverReferenceOtherServicesApiOrDomain_OnlyContracts()
    {
        // Boundary rule:
        // A service's *.Api and *.Domain projects never reference another service's *.Api or *.Domain.
        // They may reference only another service's *.Contracts.

        // 1. Inventory must never reference Requests (Api, Domain, or Contracts)
        string?[] inventoryApiRefs = InventoryApiAssembly
            .GetReferencedAssemblies()
            .Select(a => a.Name)
            .Where(n => n != null)
            .ToArray();

        inventoryApiRefs.ShouldNotContain(
            n => n!.StartsWith("BloodLink.Requests", StringComparison.Ordinal),
            "Inventory.Api must not reference any Requests assemblies."
        );

        string?[] inventoryDomainRefs = InventoryDomainAssembly
            .GetReferencedAssemblies()
            .Select(a => a.Name)
            .Where(n => n != null)
            .ToArray();

        inventoryDomainRefs.ShouldNotContain(
            n => n!.StartsWith("BloodLink.Requests", StringComparison.Ordinal),
            "Inventory.Domain must not reference any Requests assemblies."
        );

        // 2. Requests.Domain must never reference Inventory (Api, Domain, or Contracts)
        string?[] requestsDomainRefs = RequestsDomainAssembly
            .GetReferencedAssemblies()
            .Select(a => a.Name)
            .Where(n => n != null)
            .ToArray();

        requestsDomainRefs.ShouldNotContain(
            n => n!.StartsWith("BloodLink.Inventory", StringComparison.Ordinal),
            "Requests.Domain must not reference any Inventory assemblies."
        );

        // 3. Requests.Api may reference Inventory.Contracts, but NEVER Inventory.Api or Inventory.Domain
        string?[] requestsApiRefs = RequestsApiAssembly
            .GetReferencedAssemblies()
            .Select(a => a.Name)
            .Where(n => n != null)
            .ToArray();

        requestsApiRefs.ShouldNotContain(
            "BloodLink.Inventory.Api",
            "Requests.Api must never reference Inventory.Api."
        );
        requestsApiRefs.ShouldNotContain(
            "BloodLink.Inventory.Domain",
            "Requests.Api must never reference Inventory.Domain."
        );

        requestsApiRefs.ShouldContain(
            "BloodLink.Inventory.Contracts",
            "Requests.Api must reference Inventory.Contracts to consume its integration events."
        );
    }

    [Fact]
    public void MassTransitTypes_ShouldOnlyBeReferencedByMessagingInfrastructureOrRequestsSagas()
    {
        // Boundary rule:
        // Only Infrastructure/Messaging (and Requests.Api/Sagas) may reference MassTransit types.
        Type[] types = InventoryApiAssembly.GetExportedTypes();
        types.ShouldNotBeEmpty("Inventory.Api assembly must contain types to test non-vacuously.");

        TestResult result = Types
            .InAssembly(InventoryApiAssembly)
            .That()
            .DoNotResideInNamespace("BloodLink.Inventory.Api.Infrastructure.Messaging")
            .ShouldNot()
            .HaveDependencyOnAny("MassTransit")
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(
            $"MassTransit types must only be referenced in Infrastructure.Messaging: {string.Join(", ", result.FailingTypes?.Select(t => t.FullName) ?? [])}"
        );
    }
}
