using BloodLink.BuildingBlocks.Domain;
using BloodLink.Inventory.Domain.ValueObjects;

namespace BloodLink.Inventory.Domain.Model.BloodUnits.Events;

public record UnitReceivedDomainEvent(
    Guid UnitId,
    DonationNumber DonationNumber,
    BloodType BloodType,
    BloodComponent Component,
    DateTimeOffset CollectedAt,
    DateTimeOffset ExpiresAt,
    Guid BloodBankId
) : DomainEvent;
