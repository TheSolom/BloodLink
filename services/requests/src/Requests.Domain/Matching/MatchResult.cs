using BloodLink.Requests.Domain.ValueObjects;

namespace BloodLink.Requests.Domain.Matching;

public record MatchResult
{
    public bool IsSuccess { get; init; }
    public Guid? ChosenBankId { get; init; }
    public GeoLocation? ChosenBankLocation { get; init; }
    public IReadOnlyList<Guid> AssignedUnitIds { get; init; } = [];
    public double Score { get; init; }
    public NoMatchReason? Reason { get; init; }
    public string? ReasonDescription { get; init; }

    public static MatchResult Success(
        Guid bankId,
        GeoLocation bankLocation,
        IReadOnlyList<Guid> assignedUnitIds,
        double score
    ) =>
        new()
        {
            IsSuccess = true,
            ChosenBankId = bankId,
            ChosenBankLocation = bankLocation,
            AssignedUnitIds = assignedUnitIds,
            Score = score,
        };

    public static MatchResult NoMatch(NoMatchReason reason, string description) =>
        new()
        {
            IsSuccess = false,
            Reason = reason,
            ReasonDescription = description,
        };
}
