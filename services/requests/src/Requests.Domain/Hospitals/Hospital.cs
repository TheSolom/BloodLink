using BloodLink.BuildingBlocks.Domain;
using BloodLink.Requests.Domain.Errors;
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
            throw new DomainException(
                "Hospital ID cannot be empty.",
                RequestsDomainErrors.Hospital.InvalidId
            );
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException(
                "Hospital name is required.",
                RequestsDomainErrors.Hospital.NameRequired
            );
        }

        ArgumentNullException.ThrowIfNull(location);

        Name = name.Trim();
        Location = location;
    }
}
