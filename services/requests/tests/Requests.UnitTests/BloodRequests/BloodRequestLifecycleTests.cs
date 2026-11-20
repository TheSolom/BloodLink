using BloodLink.BuildingBlocks.Domain;
using BloodLink.Requests.Domain.BloodRequests;
using BloodLink.Requests.Domain.BloodRequests.Events;
using BloodLink.Requests.Domain.ValueObjects;
using BloodLink.Requests.UnitTests.Fixtures;
using Shouldly;
using Xunit;

namespace BloodLink.Requests.UnitTests.BloodRequests;

public class BloodRequestLifecycleTests
{
    private readonly FakeTimeProvider _timeProvider;
    private readonly DateTimeOffset _baseTime;
    private readonly GeoLocation _hospitalLocation = new(51.5074, -0.1278);

    public BloodRequestLifecycleTests()
    {
        _baseTime = new DateTimeOffset(2026, 11, 15, 12, 0, 0, TimeSpan.Zero);
        _timeProvider = new FakeTimeProvider(_baseTime);
    }

    [Fact]
    public void Submit_ShouldCreateRequest_InSubmittedStatus_AndRaiseSubmittedEvent()
    {
        var requestId = Guid.NewGuid();
        var hospitalId = Guid.NewGuid();
        DateTimeOffset neededBy = _baseTime.AddHours(4);

        var request = BloodRequest.Submit(
            requestId,
            hospitalId,
            _hospitalLocation,
            new BloodRequestDetails(
                BloodType.OPositive,
                BloodComponent.RedCells,
                UnitsNeeded: 3,
                RequestUrgency.Urgent,
                neededBy
            ),
            "idempotency-123",
            _timeProvider
        );

        request.Id.ShouldBe(requestId);
        request.HospitalId.ShouldBe(hospitalId);
        request.HospitalLocation.ShouldBe(_hospitalLocation);
        request.BloodType.ShouldBe(BloodType.OPositive);
        request.Component.ShouldBe(BloodComponent.RedCells);
        request.UnitsNeeded.ShouldBe(3);
        request.Urgency.ShouldBe(RequestUrgency.Urgent);
        request.NeededBy.ShouldBe(neededBy);
        request.Status.ShouldBe(BloodRequestStatus.Submitted);
        request.IdempotencyKey.ShouldBe("idempotency-123");
        request.AssignedUnitIds.ShouldBeEmpty();

        request.DomainEvents.Count.ShouldBe(1);
        BloodRequestSubmittedDomainEvent submittedEvent = request
            .DomainEvents.First()
            .ShouldBeOfType<BloodRequestSubmittedDomainEvent>();
        submittedEvent.RequestId.ShouldBe(requestId);
        submittedEvent.HospitalId.ShouldBe(hospitalId);
        submittedEvent.UnitsNeeded.ShouldBe(3);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(21)]
    [InlineData(100)]
    public void Submit_ShouldThrowDomainException_WhenUnitsNeededOutsideRange(int unitsNeeded)
    {
        Should
            .Throw<DomainException>(() =>
                BloodRequest.Submit(
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    _hospitalLocation,
                    new BloodRequestDetails(
                        BloodType.OPositive,
                        BloodComponent.RedCells,
                        unitsNeeded,
                        RequestUrgency.Routine,
                        _baseTime.AddHours(2)
                    ),
                    "key-1",
                    _timeProvider
                )
            )
            .Code.ShouldBe("request.invalid_units_needed");
    }

    [Fact]
    public void Submit_ShouldThrowDomainException_WhenNeededByIsInThePastOrNow()
    {
        Should
            .Throw<DomainException>(() =>
                BloodRequest.Submit(
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    _hospitalLocation,
                    new BloodRequestDetails(
                        BloodType.OPositive,
                        BloodComponent.RedCells,
                        UnitsNeeded: 2,
                        RequestUrgency.Routine,
                        _baseTime // same as now
                    ),
                    "key-1",
                    _timeProvider
                )
            )
            .Code.ShouldBe("request.needed_by_in_past");

        Should
            .Throw<DomainException>(() =>
                BloodRequest.Submit(
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    _hospitalLocation,
                    new BloodRequestDetails(
                        BloodType.OPositive,
                        BloodComponent.RedCells,
                        UnitsNeeded: 2,
                        RequestUrgency.Routine,
                        _baseTime.AddMinutes(-5) // in past
                    ),
                    "key-1",
                    _timeProvider
                )
            )
            .Code.ShouldBe("request.needed_by_in_past");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Submit_ShouldThrowDomainException_WhenIdempotencyKeyMissing(string key)
    {
        Should
            .Throw<DomainException>(() =>
                BloodRequest.Submit(
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    _hospitalLocation,
                    new BloodRequestDetails(
                        BloodType.OPositive,
                        BloodComponent.RedCells,
                        UnitsNeeded: 2,
                        RequestUrgency.Routine,
                        _baseTime.AddHours(2)
                    ),
                    key,
                    _timeProvider
                )
            )
            .Code.ShouldBe("request.idempotency_key_required");
    }

    [Fact]
    public void FullHappyPathLifecycle_ShouldTransitionThroughAllStates_AndRaiseDomainEvents()
    {
        BloodRequest request = CreateValidRequest(unitsNeeded: 2);

        // 1. Matching
        request.StartMatching();
        request.Status.ShouldBe(BloodRequestStatus.Matching);
        request
            .DomainEvents.OfType<BloodRequestMatchingStartedDomainEvent>()
            .ShouldHaveSingleItem();

        // 2. Reserving
        var unit1 = Guid.NewGuid();
        var unit2 = Guid.NewGuid();
        request.StartReserving([unit1, unit2]);
        request.Status.ShouldBe(BloodRequestStatus.Reserving);
        request.AssignedUnitIds.ShouldBe([unit1, unit2]);
        request
            .DomainEvents.OfType<BloodRequestReservingStartedDomainEvent>()
            .ShouldHaveSingleItem();

        // 3. Dispatching
        request.StartDispatching();
        request.Status.ShouldBe(BloodRequestStatus.Dispatching);
        request
            .DomainEvents.OfType<BloodRequestDispatchingStartedDomainEvent>()
            .ShouldHaveSingleItem();

        // 4. InTransit
        request.MarkInTransit();
        request.Status.ShouldBe(BloodRequestStatus.InTransit);
        request.DomainEvents.OfType<BloodRequestInTransitDomainEvent>().ShouldHaveSingleItem();

        // 5. Fulfilled
        request.MarkFulfilled();
        request.Status.ShouldBe(BloodRequestStatus.Fulfilled);
        request.DomainEvents.OfType<BloodRequestFulfilledDomainEvent>().ShouldHaveSingleItem();
    }

    [Fact]
    public void StartReserving_ShouldThrowDomainException_WhenAssignedUnitsCountDoesNotMatchUnitsNeeded()
    {
        BloodRequest request = CreateValidRequest(unitsNeeded: 2);
        request.StartMatching();

        Should
            .Throw<DomainException>(() =>
                request.StartReserving([Guid.NewGuid()]) // only 1 unit provided, need 2
            )
            .Code.ShouldBe("request.unit_count_mismatch");
    }

    [Fact]
    public void Cancel_ShouldBeAllowed_BeforeInTransit()
    {
        // Cancel from Submitted
        BloodRequest req1 = CreateValidRequest();
        req1.Cancel("Patient transferred");
        req1.Status.ShouldBe(BloodRequestStatus.Cancelled);

        // Cancel from Matching
        BloodRequest req2 = CreateValidRequest();
        req2.StartMatching();
        req2.Cancel("Procedure postponed");
        req2.Status.ShouldBe(BloodRequestStatus.Cancelled);

        // Cancel from Reserving
        BloodRequest req3 = CreateValidRequest(unitsNeeded: 1);
        req3.StartMatching();
        req3.StartReserving([Guid.NewGuid()]);
        req3.Cancel("No longer needed");
        req3.Status.ShouldBe(BloodRequestStatus.Cancelled);

        // Cancel from Dispatching
        BloodRequest req4 = CreateValidRequest(unitsNeeded: 1);
        req4.StartMatching();
        req4.StartReserving([Guid.NewGuid()]);
        req4.StartDispatching();
        req4.Cancel("Surgeon cancelled surgery");
        req4.Status.ShouldBe(BloodRequestStatus.Cancelled);
    }

    [Fact]
    public void Cancel_ShouldThrowDomainException_WhenInTransitOrTerminal()
    {
        // When InTransit
        BloodRequest reqInTransit = CreateValidRequest(unitsNeeded: 1);
        reqInTransit.StartMatching();
        reqInTransit.StartReserving([Guid.NewGuid()]);
        reqInTransit.StartDispatching();
        reqInTransit.MarkInTransit();

        Should
            .Throw<DomainException>(() => reqInTransit.Cancel("Too late"))
            .Code.ShouldBe("request.cannot_cancel_in_transit");

        // When Fulfilled
        reqInTransit.MarkFulfilled();
        Should
            .Throw<DomainException>(() => reqInTransit.Cancel("Already fulfilled"))
            .Code.ShouldBe("request.cannot_cancel_terminal");

        // When Failed
        BloodRequest reqFailed = CreateValidRequest();
        reqFailed.MarkFailed("No stock");
        Should
            .Throw<DomainException>(() => reqFailed.Cancel("Already failed"))
            .Code.ShouldBe("request.cannot_cancel_terminal");

        // When Cancelled
        BloodRequest reqCancelled = CreateValidRequest();
        reqCancelled.Cancel("First cancel");
        Should
            .Throw<DomainException>(() => reqCancelled.Cancel("Second cancel"))
            .Code.ShouldBe("request.cannot_cancel_terminal");
    }

    [Fact]
    public void MarkFailed_ShouldTransitionToFailed_FromNonTerminalStates()
    {
        BloodRequest request = CreateValidRequest();
        request.MarkFailed("No match found");

        request.Status.ShouldBe(BloodRequestStatus.Failed);
        request
            .DomainEvents.OfType<BloodRequestFailedDomainEvent>()
            .ShouldHaveSingleItem()
            .Reason.ShouldBe("No match found");
    }

    private BloodRequest CreateValidRequest(int unitsNeeded = 1)
    {
        return BloodRequest.Submit(
            Guid.NewGuid(),
            Guid.NewGuid(),
            _hospitalLocation,
            new BloodRequestDetails(
                BloodType.OPositive,
                BloodComponent.RedCells,
                unitsNeeded,
                RequestUrgency.Routine,
                _baseTime.AddHours(5)
            ),
            $"key-{Guid.NewGuid():N}",
            _timeProvider
        );
    }
}
