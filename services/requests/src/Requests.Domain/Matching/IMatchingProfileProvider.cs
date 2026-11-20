using BloodLink.Requests.Domain.BloodRequests;

namespace BloodLink.Requests.Domain.Matching;

public interface IMatchingProfileProvider
{
    MatchingWeightProfile GetProfile(RequestUrgency urgency);
}
