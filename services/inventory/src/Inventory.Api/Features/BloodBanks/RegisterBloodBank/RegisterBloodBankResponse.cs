namespace BloodLink.Inventory.Api.Features.BloodBanks.RegisterBloodBank;

public record RegisterBloodBankResponse(
    Guid Id,
    string Name,
    double Latitude,
    double Longitude
);
