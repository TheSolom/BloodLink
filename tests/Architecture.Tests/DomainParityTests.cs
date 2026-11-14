using Shouldly;
using Xunit;
using InventoryBloodType = BloodLink.Inventory.Domain.ValueObjects.BloodType;
using InventoryComponentExtensions = BloodLink.Inventory.Domain.ValueObjects.BloodComponentExtensions;
using RequestsBloodType = BloodLink.Requests.Domain.ValueObjects.BloodType;
using RequestsComponent = BloodLink.Requests.Domain.ValueObjects.BloodComponent;

namespace BloodLink.Architecture.Tests;

public class DomainParityTests
{
    private static readonly string[] CanonicalBloodTypeStrings =
    [
        "A+",
        "A-",
        "B+",
        "B-",
        "AB+",
        "AB-",
        "O+",
        "O-",
    ];

    private static readonly string[] ValidComponentNames = ["RedCells", "Platelets"];

    [Fact]
    public void BothDomains_MustAgreeOnCanonicalBloodTypeStrings()
    {
        // Requests domain canonical strings
        string[] requestsStrings = RequestsBloodType
            .All.Select(bt => bt.ToString())
            .OrderBy(s => s)
            .ToArray();
        // Canonical strings defined in test
        string[] expectedStrings = CanonicalBloodTypeStrings.OrderBy(s => s).ToArray();

        requestsStrings.ShouldBe(expectedStrings);

        // Check Inventory domain by parsing all canonical strings
        foreach (string canonical in CanonicalBloodTypeStrings)
        {
            InventoryBloodType.TryParse(canonical, out InventoryBloodType? invBt).ShouldBeTrue();
            invBt.ToString().ShouldBe(canonical);
        }
    }

    [Theory]
    [InlineData("A+")]
    [InlineData("A-")]
    [InlineData("B+")]
    [InlineData("B-")]
    [InlineData("AB+")]
    [InlineData("AB-")]
    [InlineData("O+")]
    [InlineData("O-")]
    [InlineData("a+")]
    [InlineData("o-")]
    [InlineData("ab+")]
    [InlineData("O\u2212")] // Typographic minus (U+2212) must be rejected by both
    [InlineData("A\u2212")] // Typographic minus (U+2212) must be rejected by both
    [InlineData("O NEGATIVE")] // Long name must be rejected by both
    [InlineData("ONEGATIVE")]
    [InlineData("O POSITIVE")]
    [InlineData("XYZ")]
    [InlineData("")]
    [InlineData(" ")]
    public void BothDomains_MustAgreeOnBloodTypeParsingBehavior(string input)
    {
        bool invSuccess = InventoryBloodType.TryParse(input, out InventoryBloodType? invBt);
        bool reqSuccess = RequestsBloodType.TryParse(input, out RequestsBloodType? reqBt);

        reqSuccess.ShouldBe(
            invSuccess,
            $"Mismatch on TryParse for input '{input}': Inventory={invSuccess}, Requests={reqSuccess}"
        );

        if (invSuccess && reqSuccess)
        {
            reqBt!
                .ToString()
                .ShouldBe(invBt!.ToString(), $"Mismatch on formatted string for input '{input}'");
        }
    }

    [Fact]
    public void BothDomains_MustAgreeOnValidComponentNames()
    {
        string[] testComponents =
        [
            "RedCells",
            "Platelets",
            "Plasma",
            "WhiteCells",
            "WholeBlood",
            "Invalid",
        ];

        foreach (string comp in testComponents)
        {
            bool invValid = InventoryComponentExtensions.TryParse(comp, out _);
            bool reqValid =
                Enum.TryParse<RequestsComponent>(
                    comp,
                    ignoreCase: true,
                    out RequestsComponent parsedReq
                ) && Enum.IsDefined(parsedReq);

            reqValid.ShouldBe(
                invValid,
                $"Mismatch on component validity for '{comp}': Inventory={invValid}, Requests={reqValid}"
            );
        }
    }
}
