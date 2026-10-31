using System.Diagnostics.CodeAnalysis;

namespace BloodLink.Inventory.Domain.ValueObjects;

public enum BloodComponent
{
    Plasma,
    RedCells,
    WhiteCells,
    Platelets
}

public static class BloodComponentExtensions
{
    public static TimeSpan GetShelfLife(this BloodComponent component) =>
        component switch
        {
            BloodComponent.RedCells => TimeSpan.FromDays(42),
            BloodComponent.Platelets => TimeSpan.FromDays(5),
            _ => throw new ArgumentOutOfRangeException(nameof(component), component, "Unsupported blood component.")
        };

    public static bool TryParse(string? input, [NotNullWhen(true)] out BloodComponent component)
    {
        if (string.Equals(input, nameof(BloodComponent.RedCells), StringComparison.OrdinalIgnoreCase))
        {
            component = BloodComponent.RedCells;
            return true;
        }

        if (string.Equals(input, nameof(BloodComponent.Platelets), StringComparison.OrdinalIgnoreCase))
        {
            component = BloodComponent.Platelets;
            return true;
        }

        component = default;
        return false;
    }
}
