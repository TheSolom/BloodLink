using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace BloodLink.Requests.Api.Infrastructure.Persistence;

public class RequestsDbContextFactory : IDesignTimeDbContextFactory<RequestsDbContext>
{
    public RequestsDbContext CreateDbContext(string[] args)
    {
        string basePath = Directory.GetCurrentDirectory();

        if (!File.Exists(Path.Combine(basePath, "appsettings.Development.json")))
        {
            string projectFolder = Path.Combine(
                basePath,
                "services",
                "requests",
                "src",
                "Requests.Api"
            );
            if (File.Exists(Path.Combine(projectFolder, "appsettings.Development.json")))
            {
                basePath = projectFolder;
            }
        }

        string environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
            ?? Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
            ?? "Development";

        IConfigurationRoot configuration = new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
            .AddJsonFile($"appsettings.{environment}.json", optional: true, reloadOnChange: false)
            .AddEnvironmentVariables()
            .Build();

        string connectionString = configuration.GetConnectionString("RequestsMigrationsConnection")
            ?? throw new InvalidOperationException(
                $"Could not find connection string 'RequestsMigrationsConnection' in configuration at '{basePath}' for environment '{environment}'."
            );
        var optionsBuilder = new DbContextOptionsBuilder<RequestsDbContext>();
        optionsBuilder.UseSqlServer(connectionString);

        return new RequestsDbContext(optionsBuilder.Options);
    }
}
