using BloodLink.BuildingBlocks.Domain;
using BloodLink.Requests.Domain.ValueObjects;

namespace BloodLink.Requests.Domain.Compatibility;

public static class RedCellCompatibility
{
    private static readonly Dictionary<BloodType, HashSet<BloodType>> CompatibleRecipients = new()
    {
        [BloodType.ONegative] =
        [
            BloodType.ONegative,
            BloodType.OPositive,
            BloodType.ANegative,
            BloodType.APositive,
            BloodType.BNegative,
            BloodType.BPositive,
            BloodType.ABNegative,
            BloodType.ABPositive,
        ],
        [BloodType.OPositive] =
        [
            BloodType.OPositive,
            BloodType.APositive,
            BloodType.BPositive,
            BloodType.ABPositive,
        ],
        [BloodType.ANegative] =
        [
            BloodType.ANegative,
            BloodType.APositive,
            BloodType.ABNegative,
            BloodType.ABPositive,
        ],
        [BloodType.APositive] = [BloodType.APositive, BloodType.ABPositive],
        [BloodType.BNegative] =
        [
            BloodType.BNegative,
            BloodType.BPositive,
            BloodType.ABNegative,
            BloodType.ABPositive,
        ],
        [BloodType.BPositive] = [BloodType.BPositive, BloodType.ABPositive],
        [BloodType.ABNegative] = [BloodType.ABNegative, BloodType.ABPositive],
        [BloodType.ABPositive] = [BloodType.ABPositive],
    };

    public static bool IsCompatible(BloodType donor, BloodType recipient)
    {
        ArgumentNullException.ThrowIfNull(donor);
        ArgumentNullException.ThrowIfNull(recipient);

        return CompatibleRecipients.TryGetValue(donor, out HashSet<BloodType>? recipients)
            && recipients.Contains(recipient);
    }

    public static int GetServesCount(BloodType bloodType)
    {
        ArgumentNullException.ThrowIfNull(bloodType);

        return CompatibleRecipients.TryGetValue(bloodType, out HashSet<BloodType>? recipients)
            ? recipients.Count
            : 0;
    }

    public static IReadOnlySet<BloodType> GetCompatibleRecipients(BloodType donor)
    {
        ArgumentNullException.ThrowIfNull(donor);
        return CompatibleRecipients[donor];
    }

    public static double CalculateWaste(BloodType donor, BloodType recipient)
    {
        if (!IsCompatible(donor, recipient))
        {
            throw new DomainException(
                $"Donor '{donor}' is incompatible with recipient '{recipient}' for red cells.",
                "compatibility.incompatible"
            );
        }

        int donorServesCount = GetServesCount(donor);
        int recipientOwnServesCount = GetServesCount(recipient);

        int diff = donorServesCount - recipientOwnServesCount;
        if (diff < 0)
        {
            throw new DomainException(
                $"Serves count difference cannot be negative for compatible donor '{donor}' and recipient '{recipient}'.",
                "compatibility.negative_waste"
            );
        }

        return diff / 7.0;
    }
}
