using BloodLink.BuildingBlocks.Domain;
using BloodLink.Inventory.Domain.ValueObjects;

namespace BloodLink.Inventory.Domain.Model.BloodBanks.Events;

public record BloodBankRegisteredDomainEvent(
    Guid BloodBankId,
    string Name,
    GeoLocation Location
) : DomainEvent;
