using BloodLink.Inventory.Api.Infrastructure.Messaging;
using BloodLink.Inventory.Domain.Model.BloodBanks;
using BloodLink.Inventory.Domain.Model.BloodUnits;
using Microsoft.EntityFrameworkCore;

namespace BloodLink.Inventory.Api.Infrastructure.Persistence;

public class InventoryDbContext(DbContextOptions<InventoryDbContext> options) : DbContext(options)
{
    public DbSet<BloodUnit> BloodUnits => Set<BloodUnit>();
    public DbSet<BloodBank> BloodBanks => Set<BloodBank>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("inventory");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(InventoryDbContext).Assembly);
        modelBuilder.ConfigureInventoryOutbox();
        base.OnModelCreating(modelBuilder);
    }
}
