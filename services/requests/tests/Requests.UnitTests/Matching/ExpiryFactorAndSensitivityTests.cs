using BloodLink.Requests.Domain.BloodRequests;
using BloodLink.Requests.Domain.Matching;
using BloodLink.Requests.Domain.ValueObjects;
using BloodLink.Requests.UnitTests.Fixtures;
using Shouldly;
using Xunit;

namespace BloodLink.Requests.UnitTests.Matching;

public class ExpiryFactorAndSensitivityTests
{
    private readonly DateTimeOffset _now = new(2026, 11, 20, 12, 0, 0, TimeSpan.Zero);
    private readonly GeoLocation _hospitalLocation = new(51.5074, -0.1278);
    private readonly FakeTimeProvider _timeProvider;
    private readonly WeightedMatchingPolicy _matchingPolicy;

    public ExpiryFactorAndSensitivityTests()
    {
        _timeProvider = new FakeTimeProvider(_now);
        _matchingPolicy = new WeightedMatchingPolicy(new MatchingProfileProvider());
    }

    [Fact]
    public void TestA_Routine_FreshONegative_Vs_AvailableOPositive_ForOPositivePatient_OPositiveWins()
    {
        // (a) Routine, fresh O- vs available O+ for an O+ patient: O+ wins
        // Both units are fresh (outside the 20% window of 8.4 days).
        // O- expires in 20 days, O+ expires in 30 days. Both have expiryFactor = 0.
        // Waste penalty for O- (4/7) means O+ wins, preserving the universal O- donor.
        var request = BloodRequest.Submit(
            Guid.NewGuid(),
            Guid.NewGuid(),
            _hospitalLocation,
            BloodType.OPositive,
            BloodComponent.RedCells,
            unitsNeeded: 1,
            RequestUrgency.Routine,
            _now.AddDays(5),
            "idem-test-a",
            _timeProvider
        );

        var bankId = Guid.NewGuid();
        var loc = new GeoLocation(51.5200, -0.1000);

        var freshONeg = new CandidateUnitSnapshot(
            Guid.NewGuid(),
            bankId,
            loc,
            BloodType.ONegative,
            BloodComponent.RedCells,
            ExpiresAt: _now.AddDays(20),
            Version: 1
        );

        var freshOPos = new CandidateUnitSnapshot(
            Guid.NewGuid(),
            bankId,
            loc,
            BloodType.OPositive,
            BloodComponent.RedCells,
            ExpiresAt: _now.AddDays(30),
            Version: 1
        );

        MatchResult result = _matchingPolicy.Match(
            request,
            [freshONeg, freshOPos],
            _now,
            excludedUnitIds: new HashSet<Guid>()
        );

        result.IsSuccess.ShouldBeTrue();
        result
            .AssignedUnitIds[0]
            .ShouldBe(
                freshOPos.UnitId,
                "Fresh O+ should win over fresh O- to prevent wasting universal donor stock"
            );
    }

    [Fact]
    public void TestB_Routine_InsideWindowONegative_Vs_FreshOPositive_ForOPositivePatient_ONegativeWins()
    {
        // (b) The same pair when O- is inside the window: O- wins
        // Red cells window = 0.20 * 42 days = 8.4 days.
        // O- expires in 2 days (inside window, expiryFactor ~0.76).
        // Fresh O+ expires in 30 days (outside window, expiryFactor = 0).
        // Expiry urgency on O- outweighs the waste penalty, preventing it from expiring on the shelf.
        var request = BloodRequest.Submit(
            Guid.NewGuid(),
            Guid.NewGuid(),
            _hospitalLocation,
            BloodType.OPositive,
            BloodComponent.RedCells,
            unitsNeeded: 1,
            RequestUrgency.Routine,
            _now.AddDays(5),
            "idem-test-b",
            _timeProvider
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
            ExpiresAt: _now.AddDays(30),
            Version: 1
        );

        MatchResult result = _matchingPolicy.Match(
            request,
            [nearExpiryONeg, freshOPos],
            _now,
            excludedUnitIds: new HashSet<Guid>()
        );

        result.IsSuccess.ShouldBeTrue();
        result
            .AssignedUnitIds[0]
            .ShouldBe(
                nearExpiryONeg.UnitId,
                "Near-expiry O- inside the window should win over fresh O+ to avoid discarding expired blood"
            );
    }

    [Fact]
    public void TestC_BoundaryAtExactlyWindow_FactorIsZero_OneTickInside_FactorIsGreaterThanZero()
    {
        // (c) Boundary at exactly window: factor is 0, one tick inside: > 0
        var options = new MatchingOptions { ExpiryWindowFraction = 0.20 };
        // For RedCells (42 days), window = 0.20 * 42 = 8.4 days = 201.6 hours
        TimeSpan window = TimeSpan.FromHours(42.0 * 24.0 * options.ExpiryWindowFraction);

        var unitAtBoundary = new CandidateUnitSnapshot(
            Guid.NewGuid(),
            Guid.NewGuid(),
            _hospitalLocation,
            BloodType.OPositive,
            BloodComponent.RedCells,
            ExpiresAt: _now.Add(window),
            Version: 1
        );

        var unitOneTickInside = new CandidateUnitSnapshot(
            Guid.NewGuid(),
            Guid.NewGuid(),
            _hospitalLocation,
            BloodType.OPositive,
            BloodComponent.RedCells,
            ExpiresAt: _now.Add(window).AddTicks(-1),
            Version: 1
        );

        double factorAtBoundary = WeightedMatchingPolicy.CalculateExpiryFactor(
            unitAtBoundary,
            _now,
            options
        );
        double factorOneTickInside = WeightedMatchingPolicy.CalculateExpiryFactor(
            unitOneTickInside,
            _now,
            options
        );

        factorAtBoundary.ShouldBe(0.0, "At exactly the window boundary, expiry factor must be 0");
        factorOneTickInside.ShouldBeGreaterThan(
            0.0,
            "One tick inside the window boundary, expiry factor must be strictly greater than 0"
        );
    }

    [Fact]
    public void TestD_Platelets_ScaleByTheirOwnShelfLife()
    {
        // (d) Platelets scale by their own shelf life (5 days vs 42 days)
        // With default 20% window:
        // Platelets window = 0.20 * 5 days = 1.0 day (24 hours).
        // Red cells window = 0.20 * 42 days = 8.4 days (201.6 hours).
        var options = new MatchingOptions { ExpiryWindowFraction = 0.20 };

        // Test at 36 hours (1.5 days) remaining:
        // For platelets: 36h > 24h window => factor must be exactly 0.0 (fresh)
        var plateletUnitAt36Hours = new CandidateUnitSnapshot(
            Guid.NewGuid(),
            Guid.NewGuid(),
            _hospitalLocation,
            BloodType.APositive,
            BloodComponent.Platelets,
            ExpiresAt: _now.AddHours(36),
            Version: 1
        );

        // For red cells: 36h < 201.6h window => factor must be > 0.0 (inside window)
        var redCellUnitAt36Hours = new CandidateUnitSnapshot(
            Guid.NewGuid(),
            Guid.NewGuid(),
            _hospitalLocation,
            BloodType.APositive,
            BloodComponent.RedCells,
            ExpiresAt: _now.AddHours(36),
            Version: 1
        );

        double plateletFactor = WeightedMatchingPolicy.CalculateExpiryFactor(
            plateletUnitAt36Hours,
            _now,
            options
        );
        double redCellFactor = WeightedMatchingPolicy.CalculateExpiryFactor(
            redCellUnitAt36Hours,
            _now,
            options
        );

        plateletFactor.ShouldBe(
            0.0,
            "Platelets with 36 hours remaining are outside their 24-hour window, so factor should be 0.0"
        );
        redCellFactor.ShouldBeGreaterThan(
            0.0,
            "Red cells with 36 hours remaining are inside their 201.6-hour window, so factor should be > 0.0"
        );

        // Platelets with 12 hours remaining: remainingTime / window = 12 / 24 = 0.5 => factor = 1 - 0.5 = 0.5
        var plateletUnitAt12Hours = new CandidateUnitSnapshot(
            Guid.NewGuid(),
            Guid.NewGuid(),
            _hospitalLocation,
            BloodType.APositive,
            BloodComponent.Platelets,
            ExpiresAt: _now.AddHours(12),
            Version: 1
        );

        double plateletFactorAt12Hours = WeightedMatchingPolicy.CalculateExpiryFactor(
            plateletUnitAt12Hours,
            _now,
            options
        );
        plateletFactorAt12Hours.ShouldBe(
            0.5,
            0.0001,
            "Platelets with 12 hours remaining should have expiryFactor = 1 - (12/24) = 0.5"
        );
    }

    [Fact]
    public void TestE_SensitivityTest_UrgencyProfilesProduceDifferentWinnersOnSharedStock()
    {
        // (e) Sensitivity test where the three urgency profiles produce at least two different winners on one shared stock set.
        // Patient is O+ needing 1 unit of RedCells at _hospitalLocation.
        // Bank A is very CLOSE (10 km away) but has an O- unit (waste penalty 4/7 for O+ patient).
        // Bank B is FARTHER (120 km away) but has an exact O+ unit (0 waste).
        // Under Routine: Waste weight (0.50) dominates distance (0.10) => Bank B (O+) wins.
        // Under Critical: Distance weight (0.75) dominates waste (0.15) => Bank A (O-, close) wins.

        var locA = new GeoLocation(51.5200, -0.1000); // ~10 km from hospital
        var locB = new GeoLocation(52.2053, 0.1218); // ~80-120 km from hospital (Cambridge direction)

        var bankA = Guid.NewGuid();
        var bankB = Guid.NewGuid();

        var unitA_ONeg = new CandidateUnitSnapshot(
            Guid.NewGuid(),
            bankA,
            locA,
            BloodType.ONegative,
            BloodComponent.RedCells,
            ExpiresAt: _now.AddDays(25), // Fresh
            Version: 1
        );

        var unitB_OPos = new CandidateUnitSnapshot(
            Guid.NewGuid(),
            bankB,
            locB,
            BloodType.OPositive,
            BloodComponent.RedCells,
            ExpiresAt: _now.AddDays(25), // Fresh
            Version: 1
        );

        var sharedStock = new List<CandidateUnitSnapshot> { unitA_ONeg, unitB_OPos };

        var routineRequest = BloodRequest.Submit(
            Guid.NewGuid(),
            Guid.NewGuid(),
            _hospitalLocation,
            BloodType.OPositive,
            BloodComponent.RedCells,
            unitsNeeded: 1,
            RequestUrgency.Routine,
            _now.AddHours(24),
            "idem-routine",
            _timeProvider
        );

        var urgentRequest = BloodRequest.Submit(
            Guid.NewGuid(),
            Guid.NewGuid(),
            _hospitalLocation,
            BloodType.OPositive,
            BloodComponent.RedCells,
            unitsNeeded: 1,
            RequestUrgency.Urgent,
            _now.AddHours(12),
            "idem-urgent",
            _timeProvider
        );

        var criticalRequest = BloodRequest.Submit(
            Guid.NewGuid(),
            Guid.NewGuid(),
            _hospitalLocation,
            BloodType.OPositive,
            BloodComponent.RedCells,
            unitsNeeded: 1,
            RequestUrgency.Critical,
            _now.AddHours(2),
            "idem-critical",
            _timeProvider
        );

        MatchResult routineResult = _matchingPolicy.Match(
            routineRequest,
            sharedStock,
            _now,
            new HashSet<Guid>()
        );
        MatchResult urgentResult = _matchingPolicy.Match(
            urgentRequest,
            sharedStock,
            _now,
            new HashSet<Guid>()
        );
        MatchResult criticalResult = _matchingPolicy.Match(
            criticalRequest,
            sharedStock,
            _now,
            new HashSet<Guid>()
        );

        routineResult.IsSuccess.ShouldBeTrue();
        criticalResult.IsSuccess.ShouldBeTrue();

        Guid routineWinner = routineResult.ChosenBankId!.Value;
        Guid criticalWinner = criticalResult.ChosenBankId!.Value;

        var winners = new HashSet<Guid>
        {
            routineWinner,
            criticalWinner,
            urgentResult.ChosenBankId!.Value,
        };
        winners.Count.ShouldBeGreaterThanOrEqualTo(
            2,
            "The three urgency profiles must produce at least two different winners on this shared stock set"
        );

        routineWinner.ShouldBe(
            bankB,
            "Routine urgency should choose Bank B with compatible O+ to avoid wasting O-"
        );
        criticalWinner.ShouldBe(
            bankA,
            "Critical urgency should choose Bank A because it is much closer, prioritizing immediate delivery"
        );
    }
}
