using BloodLink.Inventory.Domain.Model.BloodBanks;
using Riok.Mapperly.Abstractions;

namespace BloodLink.Inventory.Api.Features.BloodBanks.RegisterBloodBank;

[Mapper]
public static partial class RegisterBloodBankMapper
{
    [MapProperty(nameof(BloodBank.Location.Latitude), nameof(RegisterBloodBankResponse.Latitude))]
    [MapProperty(nameof(BloodBank.Location.Longitude), nameof(RegisterBloodBankResponse.Longitude))]
    public static partial RegisterBloodBankResponse ToResponse(BloodBank bank);
}
