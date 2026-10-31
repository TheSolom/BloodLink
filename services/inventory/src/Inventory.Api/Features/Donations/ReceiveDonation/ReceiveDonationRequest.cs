namespace BloodLink.Inventory.Api.Features.Donations.ReceiveDonation;

public record ReceiveDonationRequest(
    Guid BloodBankId,
    string DonationNumber,
    string BloodType,
    string Component,
    DateTimeOffset CollectedAt
);
