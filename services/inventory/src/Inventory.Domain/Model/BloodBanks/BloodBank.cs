using BloodLink.BuildingBlocks.Domain;
using BloodLink.Inventory.Domain.Model.BloodBanks.Events;
using BloodLink.Inventory.Domain.ValueObjects;

namespace BloodLink.Inventory.Domain.Model.BloodBanks;

public class BloodBank : AggregateRoot<Guid>
{
    public const int NameMaxLength = 200;

    public string Name { get; private set; } = null!;
    public GeoLocation Location { get; private set; } = null!;

    private BloodBank() { }

    private BloodBank(Guid id, string name, GeoLocation location)
        : base(id)
    {
        Name = name;
        Location = location;
    }

    public static BloodBank Register(Guid id, string name, GeoLocation location)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("Blood bank name is required.", "blood_bank.name_required");
        }

        string trimmedName = name.Trim();
        if (trimmedName.Length > NameMaxLength)
        {
            throw new DomainException(
                $"Blood bank name cannot exceed {NameMaxLength} characters.",
                "blood_bank.name_too_long"
            );
        }

        ArgumentNullException.ThrowIfNull(location);

        var bank = new BloodBank(id, trimmedName, location);
        bank.AddDomainEvent(new BloodBankRegisteredDomainEvent(id, trimmedName, location));

        return bank;
    }
}
