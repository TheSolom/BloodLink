using BloodLink.BuildingBlocks.Domain;
using BloodLink.Requests.Domain.BloodRequests.Events;
using BloodLink.Requests.Domain.ValueObjects;

namespace BloodLink.Requests.Domain.BloodRequests;

public class BloodRequest : AggregateRoot<Guid>
{
    private readonly List<Guid> _assignedUnitIds = [];

    public Guid HospitalId { get; private set; }
    public GeoLocation HospitalLocation { get; private set; } = null!;
    public BloodType BloodType { get; private set; } = null!;
    public BloodComponent Component { get; private set; }
    public int UnitsNeeded { get; private set; }
    public RequestUrgency Urgency { get; private set; }
    public DateTimeOffset NeededBy { get; private set; }
    public BloodRequestStatus Status { get; private set; }
    public string IdempotencyKey { get; private set; } = null!;

    public IReadOnlyList<Guid> AssignedUnitIds => _assignedUnitIds.AsReadOnly();

    private BloodRequest() { }

    private BloodRequest(
        Guid id,
        Guid hospitalId,
        GeoLocation hospitalLocation,
        BloodRequestDetails details,
        string idempotencyKey
    )
        : base(id)
    {
        HospitalId = hospitalId;
        HospitalLocation = hospitalLocation;
        BloodType = details.BloodType;
        Component = details.Component;
        UnitsNeeded = details.UnitsNeeded;
        Urgency = details.Urgency;
        NeededBy = details.NeededBy;
        Status = BloodRequestStatus.Submitted;
        IdempotencyKey = idempotencyKey;
    }

    public static BloodRequest Submit(
        Guid id,
        Guid hospitalId,
        GeoLocation hospitalLocation,
        BloodRequestDetails details,
        string idempotencyKey,
        TimeProvider timeProvider
    )
    {
        ArgumentNullException.ThrowIfNull(hospitalLocation);
        ArgumentNullException.ThrowIfNull(details);
        ArgumentNullException.ThrowIfNull(details.BloodType);
        ArgumentNullException.ThrowIfNull(timeProvider);

        if (id == Guid.Empty)
        {
            throw new DomainException("Request ID cannot be empty.", "request.invalid_id");
        }

        if (hospitalId == Guid.Empty)
        {
            throw new DomainException(
                "Hospital ID cannot be empty.",
                "request.invalid_hospital_id"
            );
        }

        if (details.UnitsNeeded is < 1 or > 20)
        {
            throw new DomainException(
                $"Units needed must be between 1 and 20. Received: {details.UnitsNeeded}.",
                "request.invalid_units_needed"
            );
        }

        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            throw new DomainException(
                "Idempotency key is required.",
                "request.idempotency_key_required"
            );
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        if (details.NeededBy <= now)
        {
            throw new DomainException(
                $"Needed-by deadline must be strictly in the future. Now: {now:O}, NeededBy: {details.NeededBy:O}.",
                "request.needed_by_in_past"
            );
        }

        var request = new BloodRequest(
            id,
            hospitalId,
            hospitalLocation,
            details,
            idempotencyKey.Trim()
        );

        request.AddDomainEvent(
            new BloodRequestSubmittedDomainEvent(
                id,
                hospitalId,
                details.BloodType,
                details.Component,
                details.UnitsNeeded,
                details.Urgency,
                details.NeededBy,
                request.IdempotencyKey
            )
        );

        return request;
    }

    public void StartMatching()
    {
        if (Status != BloodRequestStatus.Submitted)
        {
            throw new DomainException(
                $"Cannot transition to Matching from status '{Status}'.",
                "request.invalid_status_transition"
            );
        }

        Status = BloodRequestStatus.Matching;
        AddDomainEvent(new BloodRequestMatchingStartedDomainEvent(Id));
    }

    public void StartReserving(IReadOnlyList<Guid> candidateUnitIds)
    {
        ArgumentNullException.ThrowIfNull(candidateUnitIds);

        if (Status != BloodRequestStatus.Matching)
        {
            throw new DomainException(
                $"Cannot transition to Reserving from status '{Status}'.",
                "request.invalid_status_transition"
            );
        }

        if (candidateUnitIds.Count != UnitsNeeded)
        {
            throw new DomainException(
                $"Assigned units count ({candidateUnitIds.Count}) must match UnitsNeeded ({UnitsNeeded}).",
                "request.unit_count_mismatch"
            );
        }

        _assignedUnitIds.Clear();
        _assignedUnitIds.AddRange(candidateUnitIds);

        Status = BloodRequestStatus.Reserving;
        AddDomainEvent(new BloodRequestReservingStartedDomainEvent(Id, _assignedUnitIds.ToList()));
    }

    public void StartDispatching()
    {
        if (Status != BloodRequestStatus.Reserving)
        {
            throw new DomainException(
                $"Cannot transition to Dispatching from status '{Status}'.",
                "request.invalid_status_transition"
            );
        }

        Status = BloodRequestStatus.Dispatching;
        AddDomainEvent(new BloodRequestDispatchingStartedDomainEvent(Id));
    }

    public void MarkInTransit()
    {
        if (Status != BloodRequestStatus.Dispatching)
        {
            throw new DomainException(
                $"Cannot transition to InTransit from status '{Status}'.",
                "request.invalid_status_transition"
            );
        }

        Status = BloodRequestStatus.InTransit;
        AddDomainEvent(new BloodRequestInTransitDomainEvent(Id));
    }

    public void MarkFulfilled()
    {
        if (Status != BloodRequestStatus.InTransit)
        {
            throw new DomainException(
                $"Cannot transition to Fulfilled from status '{Status}'.",
                "request.invalid_status_transition"
            );
        }

        Status = BloodRequestStatus.Fulfilled;
        AddDomainEvent(new BloodRequestFulfilledDomainEvent(Id));
    }

    public void MarkFailed(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new DomainException("Failure reason is required.", "request.reason_required");
        }

        if (
            Status
            is BloodRequestStatus.Fulfilled
                or BloodRequestStatus.Failed
                or BloodRequestStatus.Cancelled
        )
        {
            throw new DomainException(
                $"Cannot fail request in terminal status '{Status}'.",
                "request.terminal_state"
            );
        }

        Status = BloodRequestStatus.Failed;
        AddDomainEvent(new BloodRequestFailedDomainEvent(Id, reason));
    }

    public void Cancel(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new DomainException(
                "Cancellation reason is required.",
                "request.reason_required"
            );
        }

        if (Status is BloodRequestStatus.InTransit)
        {
            throw new DomainException(
                "Cannot cancel request once blood units are in transit.",
                "request.cannot_cancel_in_transit"
            );
        }

        if (
            Status
            is BloodRequestStatus.Fulfilled
                or BloodRequestStatus.Failed
                or BloodRequestStatus.Cancelled
        )
        {
            throw new DomainException(
                $"Cannot cancel request in terminal status '{Status}'.",
                "request.cannot_cancel_terminal"
            );
        }

        Status = BloodRequestStatus.Cancelled;
        AddDomainEvent(new BloodRequestCancelledDomainEvent(Id, reason));
    }
}
