namespace BloodLink.Requests.Domain.Matching;

public record MatchingWeightProfile(double WasteWeight, double ExpiryWeight, double DistanceWeight)
{
    public static readonly MatchingWeightProfile Routine = new(0.50, 0.40, 0.10);
    public static readonly MatchingWeightProfile Urgent = new(0.35, 0.30, 0.35);
    public static readonly MatchingWeightProfile Critical = new(0.15, 0.10, 0.75);
}
