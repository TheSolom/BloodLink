using BloodLink.Requests.Domain.BloodRequests;

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
        _ = _profileProvider.GetProfile(request.Urgency);
        throw new NotImplementedException("Matching policy is not yet implemented.");
    }
}
