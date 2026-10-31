namespace BloodLink.Inventory.Api.Features.Donations.ReceiveDonation;

public record ReceiveDonationResponse(
    Guid Id,
    string DonationNumber,
    string BloodType,
    string Component,
    DateTimeOffset CollectedAt,
    DateTimeOffset ExpiresAt,
    string Status,
    Guid BloodBankId
);
