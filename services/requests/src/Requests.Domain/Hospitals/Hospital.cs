using BloodLink.BuildingBlocks.Domain;
using BloodLink.Requests.Domain.ValueObjects;

namespace BloodLink.Requests.Domain.Hospitals;

public class Hospital : Entity<Guid>
{
    public string Name { get; private set; } = null!;
    public GeoLocation Location { get; private set; } = null!;

    private Hospital() { }

    public Hospital(Guid id, string name, GeoLocation location)
        : base(id)
    {
        if (id == Guid.Empty)
        {
            throw new DomainException("Hospital ID cannot be empty.", "hospital.invalid_id");
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("Hospital name is required.", "hospital.name_required");
        }

        ArgumentNullException.ThrowIfNull(location);

        Name = name.Trim();
        Location = location;
    }
}
