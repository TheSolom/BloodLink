using System.Diagnostics.CodeAnalysis;
using BloodLink.BuildingBlocks.Domain;

namespace BloodLink.Inventory.Domain.ValueObjects;

public sealed record BloodType
{
    public AboType Abo { get; }
    public RhFactor Rh { get; }

    private BloodType(AboType abo, RhFactor rh)
    {
        Abo = abo;
        Rh = rh;
    }

    public static BloodType Create(AboType abo, RhFactor rh) => new(abo, rh);

    public static bool TryParse(string? input, [NotNullWhen(true)] out BloodType? bloodType)
    {
        bloodType = null;
        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        string trimmed = input.Trim();
        if (trimmed.Length < 2 || trimmed.Length > 3)
        {
            return false;
        }

        char rhChar = trimmed[^1];
        if (rhChar != '+' && rhChar != '-')
        {
            return false;
        }

        RhFactor rh = rhChar == '+' ? RhFactor.Positive : RhFactor.Negative;
        string aboPart = trimmed[..^1];

        AboType abo;
        if (string.Equals(aboPart, "A", StringComparison.OrdinalIgnoreCase))
        {
            abo = AboType.A;
        }
        else if (string.Equals(aboPart, "B", StringComparison.OrdinalIgnoreCase))
        {
            abo = AboType.B;
        }
        else if (string.Equals(aboPart, "AB", StringComparison.OrdinalIgnoreCase))
        {
            abo = AboType.AB;
        }
        else if (string.Equals(aboPart, "O", StringComparison.OrdinalIgnoreCase))
        {
            abo = AboType.O;
        }
        else
        {
            return false;
        }

        bloodType = new BloodType(abo, rh);
        return true;
    }

    public static BloodType Parse(string input)
    {
        if (!TryParse(input, out BloodType? bloodType))
        {
            throw new DomainException($"'{input}' is not a valid blood type code.", "blood_type.invalid");
        }

        return bloodType;
    }

    public override string ToString()
    {
        string aboStr = Abo switch
        {
            AboType.A => "A",
            AboType.B => "B",
            AboType.AB => "AB",
            AboType.O => "O",
            _ => throw new InvalidOperationException("Unsupported ABO type.")
        };

        string rhStr = Rh == RhFactor.Positive ? "+" : "-";
        return $"{aboStr}{rhStr}";
    }
}
