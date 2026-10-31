using BloodLink.Inventory.Api.Infrastructure.Persistence;
using BloodLink.Inventory.Domain.Model.BloodBanks;
using BloodLink.Inventory.Domain.ValueObjects;

namespace BloodLink.Inventory.Api.Features.BloodBanks.RegisterBloodBank;

public class RegisterBloodBankHandler(InventoryDbContext dbContext)
{
    public async Task<RegisterBloodBankResponse> HandleAsync(RegisterBloodBankRequest request, CancellationToken cancellationToken)
    {
        var location = GeoLocation.Create(request.Latitude, request.Longitude);
        var bank = BloodBank.Register(Guid.NewGuid(), request.Name, location);

        dbContext.BloodBanks.Add(bank);
        await dbContext.SaveChangesAsync(cancellationToken);

        return RegisterBloodBankMapper.ToResponse(bank);
    }
}
