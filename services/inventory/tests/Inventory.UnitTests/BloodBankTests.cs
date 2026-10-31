using BloodLink.BuildingBlocks.Domain;
using BloodLink.Inventory.Domain.Model.BloodBanks;
using BloodLink.Inventory.Domain.Model.BloodBanks.Events;
using BloodLink.Inventory.Domain.ValueObjects;
using Shouldly;
using Xunit;

namespace BloodLink.Inventory.UnitTests;

public class BloodBankTests
{
    [Fact]
    public void Register_ValidDetails_CreatesBloodBankAndRaisesEvent()
    {
        var id = Guid.NewGuid();
        var name = "City Central Blood Bank";
        var location = GeoLocation.Create(40.7128, -74.0060);

        var bank = BloodBank.Register(id, name, location);

        bank.Id.ShouldBe(id);
        bank.Name.ShouldBe(name);
        bank.Location.ShouldBe(location);

        bank.DomainEvents.Count.ShouldBe(1);
        BloodBankRegisteredDomainEvent evt = bank
            .DomainEvents.First()
            .ShouldBeOfType<BloodBankRegisteredDomainEvent>();
        evt.BloodBankId.ShouldBe(id);
        evt.Name.ShouldBe(name);
        evt.Location.ShouldBe(location);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Register_EmptyName_ThrowsDomainException(string? name)
    {
        DomainException ex = Should.Throw<DomainException>(() =>
            BloodBank.Register(Guid.NewGuid(), name!, GeoLocation.Create(0, 0))
        );
        ex.Code.ShouldBe("blood_bank.name_required");
    }

    [Fact]
    public void Register_NameExceeds200Characters_ThrowsDomainException()
    {
        var longName = new string('A', 201);
        DomainException ex = Should.Throw<DomainException>(() =>
            BloodBank.Register(Guid.NewGuid(), longName, GeoLocation.Create(0, 0))
        );
        ex.Code.ShouldBe("blood_bank.name_too_long");
    }
}
