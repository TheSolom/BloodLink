using BloodLink.Requests.Domain.BloodRequests;

namespace BloodLink.Requests.Domain.Matching;

public interface IMatchingPolicy
{
    MatchResult Match(
        BloodRequest request,
        IReadOnlyList<CandidateUnitSnapshot> candidateStock,
        DateTimeOffset now,
        IReadOnlySet<Guid> excludedUnitIds,
        MatchingOptions? options = null
    );
}
