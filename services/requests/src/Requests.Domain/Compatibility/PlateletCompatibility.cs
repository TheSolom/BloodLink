using BloodLink.Requests.Domain.ValueObjects;

namespace BloodLink.Requests.Domain.Compatibility;

public static class PlateletCompatibility
{
    public static bool IsCompatible(BloodType donor, BloodType recipient)
    {
        ArgumentNullException.ThrowIfNull(donor);
        ArgumentNullException.ThrowIfNull(recipient);

        // Platelets allow any blood type, but exact ABO match is preferred
        return true;
    }

    public static double CalculateWaste(BloodType donor, BloodType recipient)
    {
        ArgumentNullException.ThrowIfNull(donor);
        ArgumentNullException.ThrowIfNull(recipient);

        // Waste is 0 for exact ABO match, 1 otherwise
        return donor.Abo == recipient.Abo ? 0.0 : 1.0;
    }
}
