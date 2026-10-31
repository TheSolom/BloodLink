using BloodLink.Inventory.Domain.Model.BloodUnits;
using Riok.Mapperly.Abstractions;

namespace BloodLink.Inventory.Api.Features.Donations.ReceiveDonation;

[Mapper]
public static partial class ReceiveDonationMapper
{
    [MapProperty(nameof(BloodUnit.DonationNumber.Value), nameof(ReceiveDonationResponse.DonationNumber))]
    [MapProperty(nameof(BloodUnit.BloodType), nameof(ReceiveDonationResponse.BloodType), Use = nameof(MapBloodTypeToString))]
    [MapProperty(nameof(BloodUnit.Component), nameof(ReceiveDonationResponse.Component), Use = nameof(MapComponentToString))]
    [MapProperty(nameof(BloodUnit.Status), nameof(ReceiveDonationResponse.Status), Use = nameof(MapStatusToString))]
    public static partial ReceiveDonationResponse ToResponse(BloodUnit unit);

    private static string MapBloodTypeToString(Domain.ValueObjects.BloodType bt) => bt.ToString();
    private static string MapComponentToString(Domain.ValueObjects.BloodComponent c) => c.ToString();
    private static string MapStatusToString(BloodUnitStatus s) => s.ToString();
}
