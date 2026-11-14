using System.Diagnostics.CodeAnalysis;
using BloodLink.BuildingBlocks.Domain;

namespace BloodLink.Requests.Domain.ValueObjects;

public sealed record BloodType
{
    public AboType Abo { get; }
    public RhFactor Rh { get; }

    public static readonly BloodType ONegative = new(AboType.O, RhFactor.Negative);
    public static readonly BloodType OPositive = new(AboType.O, RhFactor.Positive);
    public static readonly BloodType ANegative = new(AboType.A, RhFactor.Negative);
    public static readonly BloodType APositive = new(AboType.A, RhFactor.Positive);
    public static readonly BloodType BNegative = new(AboType.B, RhFactor.Negative);
    public static readonly BloodType BPositive = new(AboType.B, RhFactor.Positive);
    public static readonly BloodType ABNegative = new(AboType.AB, RhFactor.Negative);
    public static readonly BloodType ABPositive = new(AboType.AB, RhFactor.Positive);

    public static readonly IReadOnlyList<BloodType> All =
    [
        ONegative,
        OPositive,
        ANegative,
        APositive,
        BNegative,
        BPositive,
        ABNegative,
        ABPositive,
    ];

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
            throw new DomainException(
                $"'{input}' is not a valid blood type code.",
                "blood_type.invalid"
            );
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
            _ => throw new InvalidOperationException("Unsupported ABO type."),
        };

        string rhStr = Rh == RhFactor.Positive ? "+" : "-";
        return $"{aboStr}{rhStr}";
    }
}
