using BloodLink.Requests.Domain.BloodRequests;
using BloodLink.Requests.Domain.Matching;
using BloodLink.Requests.Domain.ValueObjects;
using BloodLink.Requests.UnitTests.Fixtures;
using Shouldly;
using Xunit;

namespace BloodLink.Requests.UnitTests.Matching;

public class MatchingScenarioTests
{
    private readonly FakeTimeProvider _timeProvider;
    private readonly DateTimeOffset _now;
    private readonly GeoLocation _hospitalLocation = new(51.5074, -0.1278); // Central London
    private readonly IMatchingProfileProvider _profileProvider;
#pragma warning disable CA1859
    private readonly IMatchingPolicy _matchingPolicy;
#pragma warning restore CA1859

    public MatchingScenarioTests()
    {
        _now = new DateTimeOffset(2026, 11, 15, 12, 0, 0, TimeSpan.Zero);
        _timeProvider = new FakeTimeProvider(_now);
        _profileProvider = new MatchingProfileProvider();
        _matchingPolicy = new WeightedMatchingPolicy(_profileProvider);
    }

    private BloodRequest CreateRequest(
        BloodType bloodType,
        BloodComponent component,
        int unitsNeeded = 1,
        RequestUrgency urgency = RequestUrgency.Routine,
        DateTimeOffset? neededBy = null,
        string? idempotencyKey = null
    )
    {
        return BloodRequest.Submit(
            Guid.NewGuid(),
            Guid.NewGuid(),
            _hospitalLocation,
            new BloodRequestDetails(
                bloodType,
                component,
                unitsNeeded,
                urgency,
                neededBy ?? _now.AddHours(24)
            ),
            idempotencyKey ?? $"idem-{Guid.NewGuid():N}",
            _timeProvider
        );
    }

    [Fact]
    public void Scenario1_ScarceONegative_NotWastedOnOPositive_WhenOPositiveExists_UnderRoutineUrgency()
    {
        // Hospital requests O+ RedCells with Routine urgency
        var request = CreateRequest(
            BloodType.OPositive,
            BloodComponent.RedCells,
            unitsNeeded: 1,
            urgency: RequestUrgency.Routine,
            idempotencyKey: "idem-scenario-1"
        );

        var bankId = Guid.NewGuid();
        var bankLocation = new GeoLocation(51.5200, -0.1000); // ~2 km away
        DateTimeOffset standardExpiry = _now.AddDays(20);

        var unitONeg = new CandidateUnitSnapshot(
            Guid.NewGuid(),
            bankId,
            bankLocation,
            BloodType.ONegative,
            BloodComponent.RedCells,
            standardExpiry,
            Version: 1
        );

        var unitOPos = new CandidateUnitSnapshot(
            Guid.NewGuid(),
            bankId,
            bankLocation,
            BloodType.OPositive,
            BloodComponent.RedCells,
            standardExpiry,
            Version: 1
        );

        MatchResult result = _matchingPolicy.Match(
            request,
            [unitONeg, unitOPos],
            _now,
            excludedUnitIds: new HashSet<Guid>()
        );

        result.IsSuccess.ShouldBeTrue();
        result.ChosenBankId.ShouldBe(bankId);
        result.AssignedUnitIds.Count.ShouldBe(1);
        result
            .AssignedUnitIds[0]
            .ShouldBe(unitOPos.UnitId, "Routine urgency must select O+ to preserve scarce O-");
    }

    [Fact]
    public void Scenario2_SameStock_UnderCriticalUrgency_PicksNearestCompatibleBank()
    {
        // Hospital requests RedCells under Critical urgency
        var request = CreateRequest(
            BloodType.OPositive,
            BloodComponent.RedCells,
            unitsNeeded: 1,
            urgency: RequestUrgency.Critical,
            neededBy: _now.AddHours(4),
            idempotencyKey: "idem-scenario-2"
        );

        var nearBankId = Guid.NewGuid();
        var nearBankLocation = new GeoLocation(51.5100, -0.1200); // ~1 km away
        var farBankId = Guid.NewGuid();
        var farBankLocation = new GeoLocation(52.4862, -1.8904); // Birmingham, ~160 km away

        DateTimeOffset standardExpiry = _now.AddDays(20);

        // Near bank has O- (universal donor, high waste if routine, but near!)
        var nearUnit = new CandidateUnitSnapshot(
            Guid.NewGuid(),
            nearBankId,
            nearBankLocation,
            BloodType.ONegative,
            BloodComponent.RedCells,
            standardExpiry,
            Version: 1
        );

        // Far bank has exact O+ match
        var farUnit = new CandidateUnitSnapshot(
            Guid.NewGuid(),
            farBankId,
            farBankLocation,
            BloodType.OPositive,
            BloodComponent.RedCells,
            standardExpiry,
            Version: 1
        );

        MatchResult result = _matchingPolicy.Match(
            request,
            [nearUnit, farUnit],
            _now,
            excludedUnitIds: new HashSet<Guid>()
        );

        result.IsSuccess.ShouldBeTrue();
        result.ChosenBankId.ShouldBe(
            nearBankId,
            "Critical urgency weights distance (0.75) heavily over waste (0.15)"
        );
        result.AssignedUnitIds[0].ShouldBe(nearUnit.UnitId);
    }

    [Fact]
    public void Scenario3_NearExpiryUnit_ThatCannotSurviveTrip_IsFilteredOut()
    {
        var request = CreateRequest(
            BloodType.OPositive,
            BloodComponent.RedCells,
            unitsNeeded: 1,
            urgency: RequestUrgency.Routine,
            neededBy: _now.AddHours(12),
            idempotencyKey: "idem-scenario-3"
        );

        var bankId = Guid.NewGuid();
        // Bank is ~100 km away. At 40 km/h, travel time is 2.5 hours. With 30 min safety margin, trip requires 3.0 hours.
        var bankLocation = new GeoLocation(52.2053, 0.1218); // Cambridge ~80-90 km away
        double distanceKm = _hospitalLocation.DistanceToKm(bankLocation);
        double travelTimeHours = distanceKm / 40.0;
        TimeSpan requiredLeadTime = TimeSpan.FromHours(travelTimeHours) + TimeSpan.FromMinutes(30);

        // Unit expires in only 1 hour (less than requiredLeadTime)
        var expiringTooSoonUnit = new CandidateUnitSnapshot(
            Guid.NewGuid(),
            bankId,
            bankLocation,
            BloodType.OPositive,
            BloodComponent.RedCells,
            ExpiresAt: _now.Add(requiredLeadTime).AddMinutes(-10),
            Version: 1
        );

        MatchResult result = _matchingPolicy.Match(
            request,
            [expiringTooSoonUnit],
            _now,
            excludedUnitIds: new HashSet<Guid>()
        );

        result.IsSuccess.ShouldBeFalse();
        result.Reason.ShouldBe(NoMatchReason.AllCompatibleUnitsExpiredOrCannotSurviveTravel);
    }

    [Fact]
    public void Scenario4_ExcludedUnits_AreSkippedOnRematch()
    {
        var request = CreateRequest(
            BloodType.OPositive,
            BloodComponent.RedCells,
            unitsNeeded: 1,
            urgency: RequestUrgency.Routine,
            idempotencyKey: "idem-scenario-4"
        );

        var bankId = Guid.NewGuid();
        var bankLocation = new GeoLocation(51.5200, -0.1000);

        var rejectedUnit = new CandidateUnitSnapshot(
            Guid.NewGuid(),
            bankId,
            bankLocation,
            BloodType.OPositive,
            BloodComponent.RedCells,
            _now.AddDays(15),
            Version: 1
        );

        var alternativeUnit = new CandidateUnitSnapshot(
            Guid.NewGuid(),
            bankId,
            bankLocation,
            BloodType.OPositive,
            BloodComponent.RedCells,
            _now.AddDays(25),
            Version: 1
        );

        // Exclude the rejected unit
        var excluded = new HashSet<Guid> { rejectedUnit.UnitId };

        MatchResult result = _matchingPolicy.Match(
            request,
            [rejectedUnit, alternativeUnit],
            _now,
            excludedUnitIds: excluded
        );

        result.IsSuccess.ShouldBeTrue();
        result.AssignedUnitIds[0].ShouldBe(alternativeUnit.UnitId);
    }

    [Fact]
    public void Scenario5_NoSingleBankCanFillEntireRequest_ReturnsNoMatch()
    {
        // Request needs 3 units
        var request = CreateRequest(
            BloodType.OPositive,
            BloodComponent.RedCells,
            unitsNeeded: 3,
            urgency: RequestUrgency.Routine,
            idempotencyKey: "idem-scenario-5"
        );

        var bankA = Guid.NewGuid();
        var bankB = Guid.NewGuid();
        var locA = new GeoLocation(51.5200, -0.1000);
        var locB = new GeoLocation(51.5300, -0.0900);

        // Bank A has 2 units, Bank B has 1 unit. Total = 3, but neither bank can fill all 3
        var unitA1 = new CandidateUnitSnapshot(
            Guid.NewGuid(),
            bankA,
            locA,
            BloodType.OPositive,
            BloodComponent.RedCells,
            _now.AddDays(20),
            1
        );
        var unitA2 = new CandidateUnitSnapshot(
            Guid.NewGuid(),
            bankA,
            locA,
            BloodType.OPositive,
            BloodComponent.RedCells,
            _now.AddDays(22),
            1
        );
        var unitB1 = new CandidateUnitSnapshot(
            Guid.NewGuid(),
            bankB,
            locB,
            BloodType.OPositive,
            BloodComponent.RedCells,
            _now.AddDays(18),
            1
        );

        MatchResult result = _matchingPolicy.Match(
            request,
            [unitA1, unitA2, unitB1],
            _now,
            excludedUnitIds: new HashSet<Guid>()
        );

        result.IsSuccess.ShouldBeFalse();
        result.Reason.ShouldBe(NoMatchReason.NoBankCanFillEntireRequest);
    }

    [Fact]
    public void Scenario6_EqualScores_GiveDeterministicOrder()
    {
        var request = CreateRequest(
            BloodType.OPositive,
            BloodComponent.RedCells,
            unitsNeeded: 1,
            urgency: RequestUrgency.Routine,
            idempotencyKey: "idem-scenario-6"
        );

        // Two identical units from the same bank
        var bankId = Guid.NewGuid();
        var loc = new GeoLocation(51.5200, -0.1000);
        DateTimeOffset expiry = _now.AddDays(20);

        var id1 = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var id2 = Guid.Parse("00000000-0000-0000-0000-000000000002");

        var unit1 = new CandidateUnitSnapshot(
            id1,
            bankId,
            loc,
            BloodType.OPositive,
            BloodComponent.RedCells,
            expiry,
            1
        );
        var unit2 = new CandidateUnitSnapshot(
            id2,
            bankId,
            loc,
            BloodType.OPositive,
            BloodComponent.RedCells,
            expiry,
            1
        );

        MatchResult resultA = _matchingPolicy.Match(
            request,
            [unit1, unit2],
            _now,
            new HashSet<Guid>()
        );
        MatchResult resultB = _matchingPolicy.Match(
            request,
            [unit2, unit1],
            _now,
            new HashSet<Guid>()
        );

        resultA.IsSuccess.ShouldBeTrue();
        resultB.IsSuccess.ShouldBeTrue();
        resultA
            .AssignedUnitIds[0]
            .ShouldBe(
                resultB.AssignedUnitIds[0],
                "Tie-break must be completely deterministic regardless of input order"
            );
        resultA
            .AssignedUnitIds[0]
            .ShouldBe(id1, "Lower Guid tie-break when scores and expiry are identical");
    }

    [Fact]
    public void Scenario7_ExpiryApproachingONegative_ForOPositivePatient_DocumentsCurrentBehavior()
    {
        // Scenario 7: Expiry-approaching O- for an O+ patient.
        // We test whether a near-expiry O- (expiring in 2 days) beats a fresh O+ (expiring in 35 days)
        // under Routine urgency (weights: waste=0.50, expiry=0.40, dist=0.10).
        var request = CreateRequest(
            BloodType.OPositive,
            BloodComponent.RedCells,
            unitsNeeded: 1,
            urgency: RequestUrgency.Routine,
            neededBy: _now.AddDays(5),
            idempotencyKey: "idem-scenario-7"
        );

        var bankId = Guid.NewGuid();
        var loc = new GeoLocation(51.5200, -0.1000);

        var nearExpiryONeg = new CandidateUnitSnapshot(
            Guid.NewGuid(),
            bankId,
            loc,
            BloodType.ONegative,
            BloodComponent.RedCells,
            ExpiresAt: _now.AddDays(2),
            Version: 1
        );

        var freshOPos = new CandidateUnitSnapshot(
            Guid.NewGuid(),
            bankId,
            loc,
            BloodType.OPositive,
            BloodComponent.RedCells,
            ExpiresAt: _now.AddDays(35),
            Version: 1
        );

        MatchResult result = _matchingPolicy.Match(
            request,
            [nearExpiryONeg, freshOPos],
            _now,
            excludedUnitIds: new HashSet<Guid>()
        );

        result.IsSuccess.ShouldBeTrue();
        // The test verifies and documents the window-based expiry factor behavior:
        // Window is 20% of shelf life (8.4 days / 201.6h for red cells).
        // Near-expiry O- (2 days = 48h left, inside window):
        //   expiryFactor = 1 - (48 / 201.6) = ~0.762.
        //   waste factor = 1 - 4/7 = ~0.429.
        //   distance factor = ~0.976.
        //   score = 0.50 * 0.429 + 0.40 * 0.762 + 0.10 * 0.976 = ~0.617.
        // Fresh O+ (35 days left, outside 8.4-day window):
        //   expiryFactor = 0.0 (fresh, zero expiry bonus).
        //   waste factor = 1.0 (exact match).
        //   score = 0.50 * 1.0 + 0.40 * 0.0 + 0.10 * 0.976 = ~0.598.
        // Hence, near-expiry O- inside the 20% window wins over fresh O+, naturally rescuing
        // units nearing discard without requiring a separate artificial waste-fade curve.
        result
            .AssignedUnitIds[0]
            .ShouldBe(
                nearExpiryONeg.UnitId,
                "Under window-based expiry scoring (default 20% shelf life window), expiry bonus on a 2-day O- unit outweighs the waste penalty against a fresh 35-day O+ unit"
            );
    }

    [Fact]
    public void Scenario8_InsufficientTotalStock_ReturnsNoMatch()
    {
        var request = CreateRequest(
            BloodType.OPositive,
            BloodComponent.RedCells,
            unitsNeeded: 5,
            urgency: RequestUrgency.Routine,
            idempotencyKey: "idem-scenario-8"
        );

        var bankId = Guid.NewGuid();
        var loc = new GeoLocation(51.5200, -0.1000);

        var unit1 = new CandidateUnitSnapshot(
            Guid.NewGuid(),
            bankId,
            loc,
            BloodType.OPositive,
            BloodComponent.RedCells,
            _now.AddDays(10),
            1
        );
        var unit2 = new CandidateUnitSnapshot(
            Guid.NewGuid(),
            bankId,
            loc,
            BloodType.OPositive,
            BloodComponent.RedCells,
            _now.AddDays(12),
            1
        );

        MatchResult result = _matchingPolicy.Match(
            request,
            [unit1, unit2],
            _now,
            excludedUnitIds: new HashSet<Guid>()
        );

        result.IsSuccess.ShouldBeFalse();
        result.Reason.ShouldBe(NoMatchReason.InsufficientCompatibleStock);
    }

    [Fact]
    public void Scenario9_PlateletRules_PrefersExactAboMatch()
    {
        // Patient is A+ requesting platelets
        var request = CreateRequest(
            BloodType.APositive,
            BloodComponent.Platelets,
            unitsNeeded: 1,
            urgency: RequestUrgency.Routine,
            idempotencyKey: "idem-scenario-9"
        );

        var bankId = Guid.NewGuid();
        var loc = new GeoLocation(51.5200, -0.1000);
        DateTimeOffset standardExpiry = _now.AddDays(3);

        // Exact ABO match: A+ (waste = 0.0)
        var exactAboUnit = new CandidateUnitSnapshot(
            Guid.NewGuid(),
            bankId,
            loc,
            BloodType.APositive,
            BloodComponent.Platelets,
            standardExpiry,
            Version: 1
        );

        // Mismatched ABO: B+ (waste = 1.0)
        var mismatchedAboUnit = new CandidateUnitSnapshot(
            Guid.NewGuid(),
            bankId,
            loc,
            BloodType.BPositive,
            BloodComponent.Platelets,
            standardExpiry,
            Version: 1
        );

        MatchResult result = _matchingPolicy.Match(
            request,
            [mismatchedAboUnit, exactAboUnit],
            _now,
            excludedUnitIds: new HashSet<Guid>()
        );

        result.IsSuccess.ShouldBeTrue();
        result
            .AssignedUnitIds[0]
            .ShouldBe(exactAboUnit.UnitId, "Platelet matching must prefer exact ABO match");
    }

    [Fact]
    public void Match_InvalidTransitOptions_ThrowsArgumentOutOfRangeException()
    {
        BloodRequest request = CreateRequest(BloodType.OPositive, BloodComponent.RedCells);

        Should.Throw<ArgumentOutOfRangeException>(() =>
            _matchingPolicy.Match(
                request,
                [],
                _now,
                new HashSet<Guid>(),
                new MatchingOptions { TravelSpeedKmH = 0 }
            )
        );

        Should.Throw<ArgumentOutOfRangeException>(() =>
            _matchingPolicy.Match(
                request,
                [],
                _now,
                new HashSet<Guid>(),
                new MatchingOptions { TravelSpeedKmH = -10 }
            )
        );

        Should.Throw<ArgumentOutOfRangeException>(() =>
            _matchingPolicy.Match(
                request,
                [],
                _now,
                new HashSet<Guid>(),
                new MatchingOptions { ArrivalSafetyMargin = TimeSpan.FromMinutes(-1) }
            )
        );
    }
}
