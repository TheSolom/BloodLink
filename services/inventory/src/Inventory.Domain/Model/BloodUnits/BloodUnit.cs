using BloodLink.BuildingBlocks.Domain;
using BloodLink.Inventory.Domain.Model.BloodUnits.Events;
using BloodLink.Inventory.Domain.ValueObjects;

namespace BloodLink.Inventory.Domain.Model.BloodUnits;

public class BloodUnit : AggregateRoot<Guid>
{
    public DonationNumber DonationNumber { get; private set; } = null!;
    public BloodType BloodType { get; private set; } = null!;
    public BloodComponent Component { get; private set; }
    public DateTimeOffset CollectedAt { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public BloodUnitStatus Status { get; private set; }
    public Guid BloodBankId { get; private set; }
    public Guid? CurrentReservationRequestId { get; private set; }
    public int Version { get; private set; }

    private BloodUnit() { }

    private BloodUnit(
        Guid id,
        DonationNumber donationNumber,
        BloodType bloodType,
        BloodComponent component,
        DateTimeOffset collectedAt,
        DateTimeOffset expiresAt,
        Guid bloodBankId,
        int version
    )
        : base(id)
    {
        DonationNumber = donationNumber;
        BloodType = bloodType;
        Component = component;
        CollectedAt = collectedAt;
        ExpiresAt = expiresAt;
        BloodBankId = bloodBankId;
        Status = BloodUnitStatus.Available;
        Version = version;
    }

    public static BloodUnit ReceiveDonation(
        Guid id,
        DonationNumber donationNumber,
        BloodType bloodType,
        BloodComponent component,
        DateTimeOffset collectedAt,
        Guid bloodBankId,
        DateTimeOffset now
    )
    {
        ArgumentNullException.ThrowIfNull(donationNumber);
        ArgumentNullException.ThrowIfNull(bloodType);

        if (collectedAt > now)
        {
            throw new DomainException("Donation collection time cannot be in the future.", "unit.collected_in_future");
        }

        DateTimeOffset utcCollectedAt = collectedAt.ToUniversalTime();
        DateTimeOffset expiresAt = utcCollectedAt.Add(component.GetShelfLife());

        if (expiresAt <= now)
        {
            throw new DomainException("Donation is already expired based on shelf life.", "unit.already_expired");
        }

        var unit = new BloodUnit(
            id,
            donationNumber,
            bloodType,
            component,
            utcCollectedAt,
            expiresAt,
            bloodBankId,
            version: 1
        );

        unit.AddDomainEvent(new UnitReceivedDomainEvent(
            id,
            donationNumber,
            bloodType,
            component,
            utcCollectedAt,
            expiresAt,
            bloodBankId
        ));

        return unit;
    }

    public void Reserve(Guid requestId, DateTimeOffset now)
    {
        if (Status != BloodUnitStatus.Available)
        {
            throw new DomainException("Unit is not available for reservation.", "unit.not_available");
        }

        if (now >= ExpiresAt)
        {
            throw new DomainException("Cannot reserve an expired unit.", "unit.expired");
        }

        Status = BloodUnitStatus.Reserved;
        CurrentReservationRequestId = requestId;
        Version++;

        AddDomainEvent(new UnitReservedDomainEvent(Id, requestId));
    }

    public void ReleaseReservation(Guid requestId)
    {
        if (Status != BloodUnitStatus.Reserved || CurrentReservationRequestId != requestId)
        {
            throw new DomainException("Unit is not reserved by this request.", "unit.invalid_reservation");
        }

        Status = BloodUnitStatus.Available;
        CurrentReservationRequestId = null;
        Version++;

        AddDomainEvent(new UnitReservationReleasedDomainEvent(Id, requestId));
    }

    public void Expire(DateTimeOffset now)
    {
        if (Status is BloodUnitStatus.Delivered or BloodUnitStatus.Discarded)
        {
            throw new DomainException("Cannot expire unit in terminal state.", "unit.cannot_expire_terminal");
        }

        if (now < ExpiresAt)
        {
            throw new DomainException("Cannot expire unit before its expiration timestamp.", "unit.not_yet_expired");
        }

        Status = BloodUnitStatus.Expired;
        Version++;

        AddDomainEvent(new UnitExpiredDomainEvent(Id));
    }

    public void Discard(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new DomainException("Discard reason is required.", "unit.discard_reason_required");
        }

        Status = BloodUnitStatus.Discarded;
        Version++;
    }

    public void MarkInTransit(Guid requestId)
    {
        if (Status != BloodUnitStatus.Reserved || CurrentReservationRequestId != requestId)
        {
            throw new DomainException("Unit must be reserved by the request to mark in transit.", "unit.not_reserved");
        }

        Status = BloodUnitStatus.InTransit;
        Version++;

        AddDomainEvent(new UnitDispatchedDomainEvent(Id, requestId));
    }

    public void MarkDelivered(Guid requestId)
    {
        if (Status != BloodUnitStatus.InTransit || CurrentReservationRequestId != requestId)
        {
            throw new DomainException("Unit must be in transit to mark delivered.", "unit.not_in_transit");
        }

        Status = BloodUnitStatus.Delivered;
        Version++;

        AddDomainEvent(new UnitDeliveredDomainEvent(Id, requestId));
    }
}
