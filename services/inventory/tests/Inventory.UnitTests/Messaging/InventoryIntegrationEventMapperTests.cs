using BloodLink.Inventory.Api.Infrastructure.Messaging.Mappers;
using BloodLink.Inventory.Contracts;
using BloodLink.Inventory.Domain.Model.BloodUnits;
using BloodLink.Inventory.Domain.Model.BloodUnits.Events;
using BloodLink.Inventory.Domain.ValueObjects;
using Shouldly;
using Xunit;

namespace BloodLink.Inventory.UnitTests.Messaging;

public class InventoryIntegrationEventMapperTests
{
    [Fact]
    public void ToIntegrationEvent_UnitReceivedDomainEvent_ShouldMapAllFieldsAccurately()
    {
        Guid unitId = Guid.NewGuid();
        Guid bloodBankId = Guid.NewGuid();
        DateTimeOffset collectedAt = new(2026, 11, 13, 10, 0, 0, TimeSpan.Zero);
        DateTimeOffset expiresAt = collectedAt.AddDays(42);
        DonationNumber donationNumber = DonationNumber.Create("DON-12345678");
        BloodType bloodType = BloodType.Parse("O+");
        BloodComponent component = BloodComponent.RedCells;

        UnitReceivedDomainEvent domainEvent = new(
            unitId,
            donationNumber,
            bloodType,
            component,
            collectedAt,
            expiresAt,
            bloodBankId
        );

        UnitReceivedIntegrationEvent integrationEvent =
            InventoryIntegrationEventMapper.ToIntegrationEvent(domainEvent);

        integrationEvent.UnitId.ShouldBe(unitId);
        integrationEvent.BloodType.ShouldBe("O+");
        integrationEvent.Component.ShouldBe("RedCells");
        integrationEvent.CollectedAt.ShouldBe(collectedAt);
        integrationEvent.ExpiresAt.ShouldBe(expiresAt);
        integrationEvent.BloodBankId.ShouldBe(bloodBankId);
    }

    [Fact]
    public void ToUnitsReservedEvent_ShouldMapRequestIdAndUnitIds()
    {
        Guid requestId = Guid.NewGuid();
        List<Guid> unitIds = [Guid.NewGuid(), Guid.NewGuid()];

        UnitsReservedIntegrationEvent integrationEvent =
            InventoryIntegrationEventMapper.ToUnitsReservedEvent(requestId, unitIds);

        integrationEvent.RequestId.ShouldBe(requestId);
        integrationEvent.UnitIds.ShouldBe(unitIds);
    }

    [Fact]
    public void ToUnitsReleasedEvent_ShouldMapRequestIdAndUnitIds()
    {
        Guid requestId = Guid.NewGuid();
        List<Guid> unitIds = [Guid.NewGuid(), Guid.NewGuid()];

        UnitsReleasedIntegrationEvent integrationEvent =
            InventoryIntegrationEventMapper.ToUnitsReleasedEvent(requestId, unitIds);

        integrationEvent.RequestId.ShouldBe(requestId);
        integrationEvent.UnitIds.ShouldBe(unitIds);
    }

    [Fact]
    public void ToUnitExpiredEvent_ShouldMapUnitIdAndExpiredAt()
    {
        Guid unitId = Guid.NewGuid();
        UnitExpiredDomainEvent domainEvent = new(unitId);

        UnitExpiredIntegrationEvent integrationEvent =
            InventoryIntegrationEventMapper.ToUnitExpiredEvent(domainEvent);

        integrationEvent.UnitId.ShouldBe(unitId);
        integrationEvent.ExpiredAt.ShouldBe(domainEvent.OccurredOn);
    }

    [Fact]
    public void ToUnitStatusChangedEvent_ShouldMapUnitIdStatusAndVersion()
    {
        Guid unitId = Guid.NewGuid();
        string status = BloodUnitStatus.Reserved.ToString();
        int version = 3;

        UnitStatusChangedIntegrationEvent integrationEvent =
            InventoryIntegrationEventMapper.ToUnitStatusChangedEvent(unitId, status, version);

        integrationEvent.UnitId.ShouldBe(unitId);
        integrationEvent.Status.ShouldBe(status);
        integrationEvent.Version.ShouldBe(version);
    }
}
