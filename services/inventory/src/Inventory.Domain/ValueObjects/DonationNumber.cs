using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;
using BloodLink.BuildingBlocks.Domain;

namespace BloodLink.Inventory.Domain.ValueObjects;

public sealed partial record DonationNumber
{
    public string Value { get; }

    private DonationNumber(string value)
    {
        Value = value;
    }

    public static bool TryParse(string? input, [NotNullWhen(true)] out DonationNumber? donationNumber)
    {
        donationNumber = null;
        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        string trimmedUpper = input.Trim().ToUpperInvariant();
        if (trimmedUpper.Length is < 3 or > 32)
        {
            return false;
        }

        if (!ValidFormatRegex().IsMatch(trimmedUpper))
        {
            return false;
        }

        donationNumber = new DonationNumber(trimmedUpper);
        return true;
    }

    public static DonationNumber Create(string value)
    {
        if (!TryParse(value, out DonationNumber? donationNumber))
        {
            throw new DomainException($"'{value}' is not a valid donation number. Must be 3-32 alphanumeric characters with optional hyphens.", "donation_number.invalid");
        }

        return donationNumber;
    }

    public override string ToString() => Value;

    [GeneratedRegex(@"^[A-Z0-9]+(-[A-Z0-9]+)*$", RegexOptions.Compiled)]
    private static partial Regex ValidFormatRegex();
}
