using BloodLink.Requests.Domain.ValueObjects;

namespace BloodLink.Requests.Domain.Matching;

public record CandidateUnitSnapshot(
    Guid UnitId,
    Guid BankId,
    GeoLocation BankLocation,
    BloodType BloodType,
    BloodComponent Component,
    DateTimeOffset ExpiresAt,
    int Version
);
