namespace BloodLink.Requests.Domain.BloodRequests;

public enum BloodRequestStatus
{
    Submitted = 1,
    Matching = 2,
    Reserving = 3,
    Dispatching = 4,
    InTransit = 5,
    Fulfilled = 6,
    Failed = 7,
    Cancelled = 8,
}
