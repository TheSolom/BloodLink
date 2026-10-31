using BloodLink.BuildingBlocks.Domain;
using BloodLink.Inventory.Domain.Model.BloodUnits;
using BloodLink.Inventory.Domain.Model.BloodUnits.Events;
using BloodLink.Inventory.Domain.ValueObjects;
using Shouldly;
using Xunit;

namespace BloodLink.Inventory.UnitTests;

public class BloodUnitTest
{
    private readonly Guid _unitId = Guid.NewGuid();
    private readonly Guid _bankId = Guid.NewGuid();
    private readonly BloodType _bloodType = BloodType.Parse("O-");
    private readonly DonationNumber _donationNumber = DonationNumber.Create("DN-12345");
    private readonly DateTimeOffset _now = new(2026, 10, 31, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ReceiveDonation_RedCells_SetsCorrectExpiryAndRaisesEvent()
    {
        DateTimeOffset collectedAt = _now.AddDays(-1);

        var unit = BloodUnit.ReceiveDonation(
            _unitId,
            _donationNumber,
            _bloodType,
            BloodComponent.RedCells,
            collectedAt,
            _bankId,
            _now
        );

        unit.Id.ShouldBe(_unitId);
        unit.DonationNumber.ShouldBe(_donationNumber);
        unit.BloodType.ShouldBe(_bloodType);
        unit.Component.ShouldBe(BloodComponent.RedCells);
        unit.CollectedAt.ShouldBe(collectedAt);
        unit.ExpiresAt.ShouldBe(collectedAt.AddDays(42));
        unit.Status.ShouldBe(BloodUnitStatus.Available);
        unit.BloodBankId.ShouldBe(_bankId);
        unit.CurrentReservationRequestId.ShouldBeNull();
        unit.Version.ShouldBe(1);

        unit.DomainEvents.Count.ShouldBe(1);
        UnitReceivedDomainEvent evt = unit
            .DomainEvents.First()
            .ShouldBeOfType<UnitReceivedDomainEvent>();
        evt.UnitId.ShouldBe(_unitId);
        evt.DonationNumber.ShouldBe(_donationNumber);
        evt.BloodType.ShouldBe(_bloodType);
        evt.Component.ShouldBe(BloodComponent.RedCells);
        evt.CollectedAt.ShouldBe(collectedAt);
        evt.ExpiresAt.ShouldBe(collectedAt.AddDays(42));
        evt.BloodBankId.ShouldBe(_bankId);
    }

    [Fact]
    public void ReceiveDonation_Platelets_SetsCorrect5DaysExpiry()
    {
        DateTimeOffset collectedAt = _now.AddDays(-1);

        var unit = BloodUnit.ReceiveDonation(
            _unitId,
            _donationNumber,
            _bloodType,
            BloodComponent.Platelets,
            collectedAt,
            _bankId,
            _now
        );

        unit.ExpiresAt.ShouldBe(collectedAt.AddDays(5));
    }

    [Fact]
    public void ReceiveDonation_CollectedAtExactlyNow_Succeeds()
    {
        var unit = BloodUnit.ReceiveDonation(
            _unitId,
            _donationNumber,
            _bloodType,
            BloodComponent.RedCells,
            _now,
            _bankId,
            _now
        );

        unit.CollectedAt.ShouldBe(_now);
    }

    [Fact]
    public void ReceiveDonation_CollectedAtInFuture_ThrowsDomainException()
    {
        DateTimeOffset collectedInFuture = _now.AddTicks(1);

        DomainException ex = Should.Throw<DomainException>(() =>
            BloodUnit.ReceiveDonation(
                _unitId,
                _donationNumber,
                _bloodType,
                BloodComponent.RedCells,
                collectedInFuture,
                _bankId,
                _now
            )
        );
        ex.Code.ShouldBe("unit.collected_in_future");
    }

    [Fact]
    public void ReceiveDonation_ComputedExpiryExactlyNow_ThrowsDomainException()
    {
        // For Platelets (5 days), collected 5 days ago means expiry == now
        DateTimeOffset collected5DaysAgo = _now.AddDays(-5);

        DomainException ex = Should.Throw<DomainException>(() =>
            BloodUnit.ReceiveDonation(
                _unitId,
                _donationNumber,
                _bloodType,
                BloodComponent.Platelets,
                collected5DaysAgo,
                _bankId,
                _now
            )
        );
        ex.Code.ShouldBe("unit.already_expired");
    }

    [Fact]
    public void ReceiveDonation_ComputedExpiryOneTickAfterNow_Succeeds()
    {
        DateTimeOffset collected = _now.AddDays(-5).AddTicks(1);

        var unit = BloodUnit.ReceiveDonation(
            _unitId,
            _donationNumber,
            _bloodType,
            BloodComponent.Platelets,
            collected,
            _bankId,
            _now
        );

        unit.ExpiresAt.ShouldBeGreaterThan(_now);
    }

    [Fact]
    public void Reserve_WhenAvailableAndNotExpired_TransitionsToReservedAndIncrementsVersion()
    {
        BloodUnit unit = CreateAvailableUnit();
        var requestId = Guid.NewGuid();

        unit.Reserve(requestId, _now);

        unit.Status.ShouldBe(BloodUnitStatus.Reserved);
        unit.CurrentReservationRequestId.ShouldBe(requestId);
        unit.Version.ShouldBe(2);

        UnitReservedDomainEvent evt = unit.DomainEvents.OfType<UnitReservedDomainEvent>().Single();
        evt.UnitId.ShouldBe(unit.Id);
        evt.RequestId.ShouldBe(requestId);
    }

    [Fact]
    public void Reserve_WhenAlreadyReserved_ThrowsDomainException()
    {
        BloodUnit unit = CreateAvailableUnit();
        unit.Reserve(Guid.NewGuid(), _now);

        DomainException ex = Should.Throw<DomainException>(() =>
            unit.Reserve(Guid.NewGuid(), _now)
        );
        ex.Code.ShouldBe("unit.not_available");
    }

    [Fact]
    public void Reserve_WhenExpired_ThrowsDomainException()
    {
        BloodUnit unit = CreateAvailableUnit();
        DateTimeOffset afterExpiry = unit.ExpiresAt.AddSeconds(1);

        DomainException ex = Should.Throw<DomainException>(() =>
            unit.Reserve(Guid.NewGuid(), afterExpiry)
        );
        ex.Code.ShouldBe("unit.expired");
    }

    [Fact]
    public void ReleaseReservation_WhenReservedBySameRequest_RevertsToAvailableAndIncrementsVersion()
    {
        BloodUnit unit = CreateAvailableUnit();
        var requestId = Guid.NewGuid();
        unit.Reserve(requestId, _now);

        unit.ReleaseReservation(requestId);

        unit.Status.ShouldBe(BloodUnitStatus.Available);
        unit.CurrentReservationRequestId.ShouldBeNull();
        unit.Version.ShouldBe(3);

        UnitReservationReleasedDomainEvent evt = unit
            .DomainEvents.OfType<UnitReservationReleasedDomainEvent>()
            .Single();
        evt.UnitId.ShouldBe(unit.Id);
        evt.RequestId.ShouldBe(requestId);
    }

    [Fact]
    public void ReleaseReservation_WhenNotReserved_ThrowsDomainException()
    {
        BloodUnit unit = CreateAvailableUnit();

        DomainException ex = Should.Throw<DomainException>(() =>
            unit.ReleaseReservation(Guid.NewGuid())
        );
        ex.Code.ShouldBe("unit.invalid_reservation");
    }

    [Fact]
    public void ReleaseReservation_WhenMismatchedRequestId_ThrowsDomainException()
    {
        BloodUnit unit = CreateAvailableUnit();
        unit.Reserve(Guid.NewGuid(), _now);

        DomainException ex = Should.Throw<DomainException>(() =>
            unit.ReleaseReservation(Guid.NewGuid())
        );
        ex.Code.ShouldBe("unit.invalid_reservation");
    }

    [Fact]
    public void Expire_WhenPastExpiryDate_TransitionsToExpiredAndIncrementsVersion()
    {
        BloodUnit unit = CreateAvailableUnit();
        DateTimeOffset expiryCheckTime = unit.ExpiresAt.AddMinutes(1);

        unit.Expire(expiryCheckTime);

        unit.Status.ShouldBe(BloodUnitStatus.Expired);
        unit.Version.ShouldBe(2);

        UnitExpiredDomainEvent evt = unit.DomainEvents.OfType<UnitExpiredDomainEvent>().Single();
        evt.UnitId.ShouldBe(unit.Id);
    }

    [Fact]
    public void Expire_BeforeExpiryDate_ThrowsDomainException()
    {
        BloodUnit unit = CreateAvailableUnit();

        DomainException ex = Should.Throw<DomainException>(() => unit.Expire(_now));
        ex.Code.ShouldBe("unit.not_yet_expired");
    }

    [Fact]
    public void Expire_WhenAlreadyDelivered_ThrowsDomainException()
    {
        BloodUnit unit = CreateAvailableUnit();
        var req = Guid.NewGuid();
        unit.Reserve(req, _now);
        unit.MarkInTransit(req);
        unit.MarkDelivered(req);

        DomainException ex = Should.Throw<DomainException>(() =>
            unit.Expire(unit.ExpiresAt.AddDays(1))
        );
        ex.Code.ShouldBe("unit.cannot_expire_terminal");
    }

    [Fact]
    public void Discard_ValidReason_TransitionsToDiscardedAndIncrementsVersion()
    {
        BloodUnit unit = CreateAvailableUnit();

        unit.Discard("Bag compromised");

        unit.Status.ShouldBe(BloodUnitStatus.Discarded);
        unit.Version.ShouldBe(2);
    }

    [Fact]
    public void Discard_EmptyReason_ThrowsDomainException()
    {
        BloodUnit unit = CreateAvailableUnit();

        DomainException ex = Should.Throw<DomainException>(() => unit.Discard(" "));
        ex.Code.ShouldBe("unit.discard_reason_required");
    }

    [Fact]
    public void MarkInTransit_WhenReserved_TransitionsToInTransitAndRaisesEvent()
    {
        BloodUnit unit = CreateAvailableUnit();
        var requestId = Guid.NewGuid();
        unit.Reserve(requestId, _now);

        unit.MarkInTransit(requestId);

        unit.Status.ShouldBe(BloodUnitStatus.InTransit);
        unit.Version.ShouldBe(3);

        UnitDispatchedDomainEvent evt = unit
            .DomainEvents.OfType<UnitDispatchedDomainEvent>()
            .Single();
        evt.UnitId.ShouldBe(unit.Id);
        evt.RequestId.ShouldBe(requestId);
    }

    [Fact]
    public void MarkInTransit_WhenNotReserved_ThrowsDomainException()
    {
        BloodUnit unit = CreateAvailableUnit();

        DomainException ex = Should.Throw<DomainException>(() =>
            unit.MarkInTransit(Guid.NewGuid())
        );
        ex.Code.ShouldBe("unit.not_reserved");
    }

    [Fact]
    public void MarkDelivered_WhenInTransit_TransitionsToDeliveredAndRaisesEvent()
    {
        BloodUnit unit = CreateAvailableUnit();
        var requestId = Guid.NewGuid();
        unit.Reserve(requestId, _now);
        unit.MarkInTransit(requestId);

        unit.MarkDelivered(requestId);

        unit.Status.ShouldBe(BloodUnitStatus.Delivered);
        unit.Version.ShouldBe(4);

        UnitDeliveredDomainEvent evt = unit
            .DomainEvents.OfType<UnitDeliveredDomainEvent>()
            .Single();
        evt.UnitId.ShouldBe(unit.Id);
        evt.RequestId.ShouldBe(requestId);
    }

    [Fact]
    public void MarkDelivered_WhenNotInTransit_ThrowsDomainException()
    {
        BloodUnit unit = CreateAvailableUnit();
        var requestId = Guid.NewGuid();
        unit.Reserve(requestId, _now);

        DomainException ex = Should.Throw<DomainException>(() => unit.MarkDelivered(requestId));
        ex.Code.ShouldBe("unit.not_in_transit");
    }

    private BloodUnit CreateAvailableUnit() =>
        BloodUnit.ReceiveDonation(
            _unitId,
            _donationNumber,
            _bloodType,
            BloodComponent.RedCells,
            _now.AddDays(-1),
            _bankId,
            _now
        );
}
