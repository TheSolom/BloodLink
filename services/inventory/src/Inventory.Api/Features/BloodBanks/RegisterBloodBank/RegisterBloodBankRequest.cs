namespace BloodLink.Inventory.Api.Features.BloodBanks.RegisterBloodBank;

public record RegisterBloodBankRequest(
    string Name,
    double Latitude,
    double Longitude
);
