using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace BloodLink.Inventory.Api.Infrastructure.Persistence;

public class InventoryDbContextFactory : IDesignTimeDbContextFactory<InventoryDbContext>
{
    public InventoryDbContext CreateDbContext(string[] args)
    {
        string basePath = Directory.GetCurrentDirectory();

        // Support running EF CLI commands from either the solution root or project directory.
        if (!File.Exists(Path.Combine(basePath, "appsettings.Development.json")))
        {
            string projectFolder = Path.Combine(
                basePath,
                "services",
                "inventory",
                "src",
                "Inventory.Api"
            );
            if (File.Exists(Path.Combine(projectFolder, "appsettings.Development.json")))
            {
                basePath = projectFolder;
            }
        }

        string environment =
            Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
            ?? Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
            ?? "Development";

        IConfigurationRoot configuration = new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
            .AddJsonFile($"appsettings.{environment}.json", optional: true, reloadOnChange: false)
            .AddEnvironmentVariables()
            .Build();

        string connectionString =
            configuration.GetConnectionString("InventoryMigrationsConnection")
            ?? throw new InvalidOperationException(
                $"Could not find connection string 'InventoryMigrationsConnection' in configuration at '{basePath}' for environment '{environment}'."
            );

        var optionsBuilder = new DbContextOptionsBuilder<InventoryDbContext>();
        optionsBuilder.UseSqlServer(connectionString);

        return new InventoryDbContext(optionsBuilder.Options);
    }
}
