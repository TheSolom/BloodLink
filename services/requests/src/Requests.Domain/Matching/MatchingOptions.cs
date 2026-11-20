using BloodLink.Requests.Domain.BloodRequests;

namespace BloodLink.Requests.Domain.Matching;

public record MatchingOptions
{
    public double TravelSpeedKmH { get; init; } = 40.0;
    public TimeSpan ArrivalSafetyMargin { get; init; } = TimeSpan.FromMinutes(30);
    public double MaxRelevantDistanceKm { get; init; } = 500.0;
    public double ExpiryWindowFraction { get; init; } = 0.20;
    public MatchingWeightProfile RoutineWeights { get; init; } = MatchingWeightProfile.Routine;
    public MatchingWeightProfile UrgentWeights { get; init; } = MatchingWeightProfile.Urgent;
    public MatchingWeightProfile CriticalWeights { get; init; } = MatchingWeightProfile.Critical;

    public MatchingWeightProfile GetProfile(RequestUrgency urgency) =>
        urgency switch
        {
            RequestUrgency.Routine => RoutineWeights,
            RequestUrgency.Urgent => UrgentWeights,
            RequestUrgency.Critical => CriticalWeights,
            _ => RoutineWeights,
        };
}
