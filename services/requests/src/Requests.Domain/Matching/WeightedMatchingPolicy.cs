using BloodLink.Requests.Domain.BloodRequests;
using BloodLink.Requests.Domain.Compatibility;
using BloodLink.Requests.Domain.ValueObjects;

namespace BloodLink.Requests.Domain.Matching;

public class WeightedMatchingPolicy(IMatchingProfileProvider profileProvider) : IMatchingPolicy
{
    private readonly IMatchingProfileProvider _profileProvider = profileProvider;

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

        options ??= new MatchingOptions();
        MatchingWeightProfile profile = _profileProvider.GetProfile(request.Urgency);

        // 1. Initial categorization of compatibility and component
        var componentAndCompatibleUnits = new List<CandidateUnitSnapshot>();
        bool hasAnyCompatibleUnit = false;
        bool hasNonExcludedCompatibleUnit = false;

        foreach (CandidateUnitSnapshot unit in candidateStock)
        {
            if (unit.Component != request.Component)
            {
                continue;
            }

            bool isCompatible =
                unit.Component == BloodComponent.RedCells
                    ? RedCellCompatibility.IsCompatible(unit.BloodType, request.BloodType)
                    : PlateletCompatibility.IsCompatible(unit.BloodType, request.BloodType);

            if (!isCompatible)
            {
                continue;
            }

            hasAnyCompatibleUnit = true;

            if (excludedUnitIds.Contains(unit.UnitId))
            {
                continue;
            }

            hasNonExcludedCompatibleUnit = true;
            componentAndCompatibleUnits.Add(unit);
        }

        if (!hasAnyCompatibleUnit)
        {
            return MatchResult.NoMatch(
                NoMatchReason.InsufficientCompatibleStock,
                "No compatible stock found for the requested blood type and component."
            );
        }

        if (!hasNonExcludedCompatibleUnit)
        {
            return MatchResult.NoMatch(
                NoMatchReason.AllUnitsExcluded,
                "All compatible units have been excluded from consideration."
            );
        }

        // 2. Hard filter: unit must survive travel time + arrival safety margin
        var viableUnits = new List<CandidateUnitSnapshot>();
        foreach (CandidateUnitSnapshot unit in componentAndCompatibleUnits)
        {
            double distanceKm = request.HospitalLocation.DistanceToKm(unit.BankLocation);
            double travelHours = distanceKm / options.TravelSpeedKmH;
            TimeSpan estimatedTravelTime = TimeSpan.FromHours(travelHours);
            DateTimeOffset requiredArrivalDeadline = now.Add(estimatedTravelTime)
                .Add(options.ArrivalSafetyMargin);

            if (unit.ExpiresAt > requiredArrivalDeadline)
            {
                viableUnits.Add(unit);
            }
        }

        if (viableUnits.Count == 0)
        {
            return MatchResult.NoMatch(
                NoMatchReason.AllCompatibleUnitsExpiredOrCannotSurviveTravel,
                "All compatible units expire before estimated arrival time plus safety margin."
            );
        }

        if (viableUnits.Count < request.UnitsNeeded)
        {
            return MatchResult.NoMatch(
                NoMatchReason.InsufficientCompatibleStock,
                $"Total viable compatible stock ({viableUnits.Count}) is less than needed ({request.UnitsNeeded})."
            );
        }

        // 3. Single-bank MVP filter: Group by BankId
        var viableUnitsByBank = viableUnits.GroupBy(u => u.BankId).ToList();

        var qualifyingBanks = viableUnitsByBank
            .Where(g => g.Count() >= request.UnitsNeeded)
            .ToList();

        if (qualifyingBanks.Count == 0)
        {
            return MatchResult.NoMatch(
                NoMatchReason.NoBankCanFillEntireRequest,
                "No single blood bank has sufficient stock to fulfill the entire request."
            );
        }

        // 4. Soft scoring per qualifying bank
        var candidateBankEvaluations = new List<BankEvaluation>();

        foreach (IGrouping<Guid, CandidateUnitSnapshot>? bankGroup in qualifyingBanks)
        {
            Guid bankId = bankGroup.Key;
            GeoLocation bankLocation = bankGroup.First().BankLocation;

            double distanceKm = request.HospitalLocation.DistanceToKm(bankLocation);
            double distanceFactor = Math.Clamp(
                1.0 - (distanceKm / options.MaxRelevantDistanceKm),
                0.0,
                1.0
            );

            // Score each unit in the bank
            var scoredUnits = new List<ScoredUnit>();
            foreach (CandidateUnitSnapshot unit in bankGroup)
            {
                double waste =
                    unit.Component == BloodComponent.RedCells
                        ? RedCellCompatibility.CalculateWaste(unit.BloodType, request.BloodType)
                        : PlateletCompatibility.CalculateWaste(unit.BloodType, request.BloodType);

                double maxShelfLifeHours =
                    unit.Component == BloodComponent.RedCells ? (42.0 * 24.0) : (5.0 * 24.0);

                double hoursRemaining = Math.Max(0.0, (unit.ExpiresAt - now).TotalHours);
                double expiryFactor = Math.Clamp(
                    1.0 - (hoursRemaining / maxShelfLifeHours),
                    0.0,
                    1.0
                );

                double unitScore =
                    (profile.WasteWeight * (1.0 - waste))
                    + (profile.ExpiryWeight * expiryFactor)
                    + (profile.DistanceWeight * distanceFactor);

                scoredUnits.Add(new ScoredUnit(unit, unitScore));
            }

            // Select best N units for this bank
            // Order by unit score descending, earlier expiry ascending, unitId ascending
            var bestNUnits = scoredUnits
                .OrderByDescending(u => u.Score)
                .ThenBy(u => u.Snapshot.ExpiresAt)
                .ThenBy(u => u.Snapshot.UnitId)
                .Take(request.UnitsNeeded)
                .ToList();

            double averageScore = bestNUnits.Average(u => u.Score);
            DateTimeOffset earliestExpiry = bestNUnits.Min(u => u.Snapshot.ExpiresAt);

            candidateBankEvaluations.Add(
                new BankEvaluation(
                    bankId,
                    bankLocation,
                    bestNUnits.Select(u => u.Snapshot.UnitId).ToList(),
                    averageScore,
                    earliestExpiry
                )
            );
        }

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

    private sealed record ScoredUnit(CandidateUnitSnapshot Snapshot, double Score);

    private sealed record BankEvaluation(
        Guid BankId,
        GeoLocation BankLocation,
        IReadOnlyList<Guid> AssignedUnitIds,
        double Score,
        DateTimeOffset EarliestExpiry
    );
}
