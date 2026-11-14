namespace BloodLink.Requests.Domain.Matching;

public record MatchingOptions
{
    public double TravelSpeedKmH { get; init; } = 40.0;
    public TimeSpan ArrivalSafetyMargin { get; init; } = TimeSpan.FromMinutes(30);
    public double MaxRelevantDistanceKm { get; init; } = 500.0;
}
