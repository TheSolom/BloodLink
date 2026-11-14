namespace BloodLink.Requests.Domain.Matching;

public enum NoMatchReason
{
    InsufficientCompatibleStock,
    NoBankCanFillEntireRequest,
    AllCompatibleUnitsExpiredOrCannotSurviveTravel,
    AllUnitsExcluded,
}
