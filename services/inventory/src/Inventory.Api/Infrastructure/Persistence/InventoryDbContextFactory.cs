using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace BloodLink.Inventory.Api.Infrastructure.Persistence;

public class InventoryDbContextFactory : IDesignTimeDbContextFactory<InventoryDbContext>
{
    public InventoryDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<InventoryDbContext>();
        optionsBuilder.UseSqlServer("Server=localhost,1433;Database=BloodLink_Inventory;User Id=sa;Password=dummy_for_migrations_only;TrustServerCertificate=True;");

        return new InventoryDbContext(optionsBuilder.Options);
    }
}
