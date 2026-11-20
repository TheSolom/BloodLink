using BloodLink.Requests.Domain.BloodRequests;
using BloodLink.Requests.Domain.Compatibility;
using BloodLink.Requests.Domain.ValueObjects;

namespace BloodLink.Requests.Domain.Matching;

public class WeightedMatchingPolicy(IMatchingProfileProvider? profileProvider = null)
    : IMatchingPolicy
{
    private readonly IMatchingProfileProvider _profileProvider = profileProvider
        ?? new MatchingProfileProvider();

    public MatchResult Match(
        BloodRequest request,
        IReadOnlyList<CandidateUnitSnapshot> candidateStock,
        DateTimeOffset now,
        IReadOnlySet<Guid> excludedUnitIds,
        MatchingOptions? options = null
    )
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(candidateStock);
        ArgumentNullException.ThrowIfNull(excludedUnitIds);

        MatchingWeightProfile profile =
            options != null
                ? options.GetProfile(request.Urgency)
                : _profileProvider.GetProfile(request.Urgency);

        options ??= new MatchingOptions();

        // 1. Initial categorization of compatibility and component
        (List<CandidateUnitSnapshot>? compatibleUnits, MatchResult? compatibilityError) =
            FilterCompatibleStock(request, candidateStock, excludedUnitIds);
        if (compatibilityError != null)
        {
            return compatibilityError;
        }

        // 2. Hard filter: unit must survive travel time + arrival safety margin
        (List<CandidateUnitSnapshot>? viableUnits, MatchResult? viabilityError) =
            FilterViableTravelUnits(request, compatibleUnits, now, options);
        if (viabilityError != null)
        {
            return viabilityError;
        }

        // 3. Single-bank MVP filter: Group by BankId
        (
            List<IGrouping<Guid, CandidateUnitSnapshot>>? qualifyingBanks,
            MatchResult? bankStockError
        ) = GetQualifyingBanks(viableUnits, request.UnitsNeeded);
        if (bankStockError != null)
        {
            return bankStockError;
        }

        // 4. Soft scoring per qualifying bank
        var candidateBankEvaluations = qualifyingBanks
            .Select(bankGroup => EvaluateBank(bankGroup, request, now, options, profile))
            .ToList();

        // 5. Rank candidate banks deterministically:
        // Highest average score, then earlier expiry, then BankId
        BankEvaluation chosen = candidateBankEvaluations
            .OrderByDescending(b => b.Score)
            .ThenBy(b => b.EarliestExpiry)
            .ThenBy(b => b.BankId)
            .First();

        return MatchResult.Success(
            chosen.BankId,
            chosen.BankLocation,
            chosen.AssignedUnitIds,
            chosen.Score
        );
    }

    private static (
        List<CandidateUnitSnapshot> Units,
        MatchResult? ErrorResult
    ) FilterCompatibleStock(
        BloodRequest request,
        IReadOnlyList<CandidateUnitSnapshot> candidateStock,
        IReadOnlySet<Guid> excludedUnitIds
    )
    {
        var compatibleUnits = new List<CandidateUnitSnapshot>();
        bool hasAnyCompatibleUnit = false;
        bool hasNonExcludedCompatibleUnit = false;

        foreach (CandidateUnitSnapshot unit in candidateStock)
        {
            if (!IsUnitCompatible(unit, request))
            {
                continue;
            }

            hasAnyCompatibleUnit = true;

            if (excludedUnitIds.Contains(unit.UnitId))
            {
                continue;
            }

            hasNonExcludedCompatibleUnit = true;
            compatibleUnits.Add(unit);
        }

        if (!hasAnyCompatibleUnit)
        {
            return (
                compatibleUnits,
                MatchResult.NoMatch(
                    NoMatchReason.InsufficientCompatibleStock,
                    "No compatible stock found for the requested blood type and component."
                )
            );
        }

        if (!hasNonExcludedCompatibleUnit)
        {
            return (
                compatibleUnits,
                MatchResult.NoMatch(
                    NoMatchReason.AllUnitsExcluded,
                    "All compatible units have been excluded from consideration."
                )
            );
        }

        return (compatibleUnits, null);
    }

    private static bool IsUnitCompatible(CandidateUnitSnapshot unit, BloodRequest request)
    {
        if (unit.Component != request.Component)
        {
            return false;
        }

        return unit.Component == BloodComponent.RedCells
            ? RedCellCompatibility.IsCompatible(unit.BloodType, request.BloodType)
            : PlateletCompatibility.IsCompatible(unit.BloodType, request.BloodType);
    }

    private static (
        List<CandidateUnitSnapshot> Units,
        MatchResult? ErrorResult
    ) FilterViableTravelUnits(
        BloodRequest request,
        List<CandidateUnitSnapshot> compatibleUnits,
        DateTimeOffset now,
        MatchingOptions options
    )
    {
        var viableUnits = new List<CandidateUnitSnapshot>();
        foreach (CandidateUnitSnapshot unit in compatibleUnits)
        {
            if (CanSurviveTravel(request.HospitalLocation, unit, now, options))
            {
                viableUnits.Add(unit);
            }
        }

        if (viableUnits.Count == 0)
        {
            return (
                viableUnits,
                MatchResult.NoMatch(
                    NoMatchReason.AllCompatibleUnitsExpiredOrCannotSurviveTravel,
                    "All compatible units expire before estimated arrival time plus safety margin."
                )
            );
        }

        if (viableUnits.Count < request.UnitsNeeded)
        {
            return (
                viableUnits,
                MatchResult.NoMatch(
                    NoMatchReason.InsufficientCompatibleStock,
                    $"Total viable compatible stock ({viableUnits.Count}) is less than needed ({request.UnitsNeeded})."
                )
            );
        }

        return (viableUnits, null);
    }

    private static bool CanSurviveTravel(
        GeoLocation hospitalLocation,
        CandidateUnitSnapshot unit,
        DateTimeOffset now,
        MatchingOptions options
    )
    {
        double distanceKm = hospitalLocation.DistanceToKm(unit.BankLocation);
        double travelHours = distanceKm / options.TravelSpeedKmH;
        TimeSpan estimatedTravelTime = TimeSpan.FromHours(travelHours);
        DateTimeOffset requiredArrivalDeadline = now.Add(estimatedTravelTime)
            .Add(options.ArrivalSafetyMargin);

        return unit.ExpiresAt > requiredArrivalDeadline;
    }

    private static (
        List<IGrouping<Guid, CandidateUnitSnapshot>> Banks,
        MatchResult? ErrorResult
    ) GetQualifyingBanks(List<CandidateUnitSnapshot> viableUnits, int unitsNeeded)
    {
        var qualifyingBanks = viableUnits
            .GroupBy(u => u.BankId)
            .Where(g => g.Count() >= unitsNeeded)
            .ToList();

        if (qualifyingBanks.Count == 0)
        {
            return (
                qualifyingBanks,
                MatchResult.NoMatch(
                    NoMatchReason.NoBankCanFillEntireRequest,
                    "No single blood bank has sufficient stock to fulfill the entire request."
                )
            );
        }

        return (qualifyingBanks, null);
    }

    private static BankEvaluation EvaluateBank(
        IGrouping<Guid, CandidateUnitSnapshot> bankGroup,
        BloodRequest request,
        DateTimeOffset now,
        MatchingOptions options,
        MatchingWeightProfile profile
    )
    {
        Guid bankId = bankGroup.Key;
        GeoLocation bankLocation = bankGroup.First().BankLocation;

        double distanceKm = request.HospitalLocation.DistanceToKm(bankLocation);
        double distanceFactor = Math.Clamp(
            1.0 - (distanceKm / options.MaxRelevantDistanceKm),
            0.0,
            1.0
        );

        var scoredUnits = bankGroup
            .Select(unit => new ScoredUnit(
                unit,
                ScoreUnit(unit, request, now, distanceFactor, profile, options)
            ))
            .OrderByDescending(u => u.Score)
            .ThenBy(u => u.Snapshot.ExpiresAt)
            .ThenBy(u => u.Snapshot.UnitId)
            .Take(request.UnitsNeeded)
            .ToList();

        double averageScore = scoredUnits.Average(u => u.Score);
        DateTimeOffset earliestExpiry = scoredUnits.Min(u => u.Snapshot.ExpiresAt);

        return new BankEvaluation(
            bankId,
            bankLocation,
            scoredUnits.Select(u => u.Snapshot.UnitId).ToList(),
            averageScore,
            earliestExpiry
        );
    }

    private static double ScoreUnit(
        CandidateUnitSnapshot unit,
        BloodRequest request,
        DateTimeOffset now,
        double distanceFactor,
        MatchingWeightProfile profile,
        MatchingOptions options
    )
    {
        double waste = CalculateWaste(unit, request);
        double expiryFactor = CalculateExpiryFactor(unit, now, options);

        return (profile.WasteWeight * (1.0 - waste))
            + (profile.ExpiryWeight * expiryFactor)
            + (profile.DistanceWeight * distanceFactor);
    }

    private static double CalculateWaste(CandidateUnitSnapshot unit, BloodRequest request)
    {
        return unit.Component == BloodComponent.RedCells
            ? RedCellCompatibility.CalculateWaste(unit.BloodType, request.BloodType)
            : PlateletCompatibility.CalculateWaste(unit.BloodType, request.BloodType);
    }

    internal static double CalculateExpiryFactor(
        CandidateUnitSnapshot unit,
        DateTimeOffset now,
        MatchingOptions options
    )
    {
        TimeSpan shelfLife =
            unit.Component == BloodComponent.RedCells
                ? TimeSpan.FromDays(42)
                : TimeSpan.FromDays(5);

        double windowSeconds = options.ExpiryWindowFraction * shelfLife.TotalSeconds;
        if (windowSeconds <= 0)
        {
            return 0.0;
        }

        double remainingSeconds = (unit.ExpiresAt - now).TotalSeconds;
        double ratio = Math.Clamp(remainingSeconds / windowSeconds, 0.0, 1.0);
        return 1.0 - ratio;
    }

    private sealed record ScoredUnit(CandidateUnitSnapshot Snapshot, double Score);

    private sealed record BankEvaluation(
        Guid BankId,
        GeoLocation BankLocation,
        IReadOnlyList<Guid> AssignedUnitIds,
        double Score,
        DateTimeOffset EarliestExpiry
    );
}
