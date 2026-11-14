using BloodLink.Requests.Domain.Compatibility;
using BloodLink.Requests.Domain.ValueObjects;
using Shouldly;
using Xunit;

namespace BloodLink.Requests.UnitTests.Compatibility;

public class CompatibilityTests
{
    public static readonly TheoryData<BloodType, BloodType, bool> RedCellCompatibilityPairs = new()
    {
        // Donor O- can give to all 8 types
        { BloodType.ONegative, BloodType.ONegative, true },
        { BloodType.ONegative, BloodType.OPositive, true },
        { BloodType.ONegative, BloodType.ANegative, true },
        { BloodType.ONegative, BloodType.APositive, true },
        { BloodType.ONegative, BloodType.BNegative, true },
        { BloodType.ONegative, BloodType.BPositive, true },
        { BloodType.ONegative, BloodType.ABNegative, true },
        { BloodType.ONegative, BloodType.ABPositive, true },
        // Donor O+ can give to O+, A+, B+, AB+
        { BloodType.OPositive, BloodType.ONegative, false },
        { BloodType.OPositive, BloodType.OPositive, true },
        { BloodType.OPositive, BloodType.ANegative, false },
        { BloodType.OPositive, BloodType.APositive, true },
        { BloodType.OPositive, BloodType.BNegative, false },
        { BloodType.OPositive, BloodType.BPositive, true },
        { BloodType.OPositive, BloodType.ABNegative, false },
        { BloodType.OPositive, BloodType.ABPositive, true },
        // Donor A- can give to A-, A+, AB-, AB+
        { BloodType.ANegative, BloodType.ONegative, false },
        { BloodType.ANegative, BloodType.OPositive, false },
        { BloodType.ANegative, BloodType.ANegative, true },
        { BloodType.ANegative, BloodType.APositive, true },
        { BloodType.ANegative, BloodType.BNegative, false },
        { BloodType.ANegative, BloodType.BPositive, false },
        { BloodType.ANegative, BloodType.ABNegative, true },
        { BloodType.ANegative, BloodType.ABPositive, true },
        // Donor A+ can give to A+, AB+
        { BloodType.APositive, BloodType.ONegative, false },
        { BloodType.APositive, BloodType.OPositive, false },
        { BloodType.APositive, BloodType.ANegative, false },
        { BloodType.APositive, BloodType.APositive, true },
        { BloodType.APositive, BloodType.BNegative, false },
        { BloodType.APositive, BloodType.BPositive, false },
        { BloodType.APositive, BloodType.ABNegative, false },
        { BloodType.APositive, BloodType.ABPositive, true },
        // Donor B- can give to B-, B+, AB-, AB+
        { BloodType.BNegative, BloodType.ONegative, false },
        { BloodType.BNegative, BloodType.OPositive, false },
        { BloodType.BNegative, BloodType.ANegative, false },
        { BloodType.BNegative, BloodType.APositive, false },
        { BloodType.BNegative, BloodType.BNegative, true },
        { BloodType.BNegative, BloodType.BPositive, true },
        { BloodType.BNegative, BloodType.ABNegative, true },
        { BloodType.BNegative, BloodType.ABPositive, true },
        // Donor B+ can give to B+, AB+
        { BloodType.BPositive, BloodType.ONegative, false },
        { BloodType.BPositive, BloodType.OPositive, false },
        { BloodType.BPositive, BloodType.ANegative, false },
        { BloodType.BPositive, BloodType.APositive, false },
        { BloodType.BPositive, BloodType.BNegative, false },
        { BloodType.BPositive, BloodType.BPositive, true },
        { BloodType.BPositive, BloodType.ABNegative, false },
        { BloodType.BPositive, BloodType.ABPositive, true },
        // Donor AB- can give to AB-, AB+
        { BloodType.ABNegative, BloodType.ONegative, false },
        { BloodType.ABNegative, BloodType.OPositive, false },
        { BloodType.ABNegative, BloodType.ANegative, false },
        { BloodType.ABNegative, BloodType.APositive, false },
        { BloodType.ABNegative, BloodType.BNegative, false },
        { BloodType.ABNegative, BloodType.BPositive, false },
        { BloodType.ABNegative, BloodType.ABNegative, true },
        { BloodType.ABNegative, BloodType.ABPositive, true },
        // Donor AB+ can give to AB+ only
        { BloodType.ABPositive, BloodType.ONegative, false },
        { BloodType.ABPositive, BloodType.OPositive, false },
        { BloodType.ABPositive, BloodType.ANegative, false },
        { BloodType.ABPositive, BloodType.APositive, false },
        { BloodType.ABPositive, BloodType.BNegative, false },
        { BloodType.ABPositive, BloodType.BPositive, false },
        { BloodType.ABPositive, BloodType.ABNegative, false },
        { BloodType.ABPositive, BloodType.ABPositive, true },
    };

    [Theory]
    [MemberData(nameof(RedCellCompatibilityPairs))]
    public void RedCellCompatibility_ShouldExhaustivelyMatchPlanSpecification(
        BloodType donor,
        BloodType recipient,
        bool expectedCompatible
    )
    {
        RedCellCompatibility.IsCompatible(donor, recipient).ShouldBe(expectedCompatible);
    }

    [Fact]
    public void RedCellCompatibility_ExhaustiveCount_ShouldBeExactly64Pairs()
    {
        int count = 0;
        foreach (BloodType donor in BloodType.All)
        {
            foreach (BloodType recipient in BloodType.All)
            {
                count++;
            }
        }

        count.ShouldBe(64);
    }

    [Fact]
    public void RedCellCompatibility_ServesCountDifference_ShouldNeverBeNegative_ForAnyCompatiblePair()
    {
        foreach (BloodType donor in BloodType.All)
        {
            foreach (BloodType recipient in BloodType.All)
            {
                if (RedCellCompatibility.IsCompatible(donor, recipient))
                {
                    int donorServes = RedCellCompatibility.GetServesCount(donor);
                    int recipientServes = RedCellCompatibility.GetServesCount(recipient);

                    int diff = donorServes - recipientServes;
                    diff.ShouldBeGreaterThanOrEqualTo(
                        0,
                        $"Donor {donor} (serves {donorServes}) to Recipient {recipient} (serves {recipientServes}) had negative diff: {diff}"
                    );

                    double waste = RedCellCompatibility.CalculateWaste(donor, recipient);
                    waste.ShouldBeGreaterThanOrEqualTo(0.0);
                    waste.ShouldBeLessThanOrEqualTo(1.0);
                }
            }
        }
    }

    [Fact]
    public void PlateletCompatibility_ShouldAllowAnyDonor_AndScoreZeroWasteForExactAboMatch()
    {
        foreach (BloodType donor in BloodType.All)
        {
            foreach (BloodType recipient in BloodType.All)
            {
                PlateletCompatibility.IsCompatible(donor, recipient).ShouldBeTrue();

                double waste = PlateletCompatibility.CalculateWaste(donor, recipient);
                if (donor.Abo == recipient.Abo)
                {
                    waste.ShouldBe(0.0);
                }
                else
                {
                    waste.ShouldBe(1.0);
                }
            }
        }
    }
}
