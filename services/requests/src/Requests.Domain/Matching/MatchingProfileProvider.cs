using BloodLink.Requests.Domain.BloodRequests;

namespace BloodLink.Requests.Domain.Matching;

public class MatchingProfileProvider : IMatchingProfileProvider
{
    public MatchingWeightProfile GetProfile(RequestUrgency urgency) =>
        urgency switch
        {
            RequestUrgency.Routine => MatchingWeightProfile.Routine,
            RequestUrgency.Urgent => MatchingWeightProfile.Urgent,
            RequestUrgency.Critical => MatchingWeightProfile.Critical,
            _ => MatchingWeightProfile.Routine,
        };
}
