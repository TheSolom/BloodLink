using BloodLink.BuildingBlocks.Domain;
using BloodLink.Inventory.Api.Infrastructure.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace BloodLink.Inventory.IntegrationTests.Fixtures;

public class InventoryApiFactory(DatabaseFixture databaseFixture) : WebApplicationFactory<Program>
{
    public FakeTimeProvider TimeProvider { get; } =
        new(new DateTimeOffset(2026, 10, 31, 12, 0, 0, TimeSpan.Zero));

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureTestServices(services =>
        {
            // Replace DbContext configuration with the container connection string
            services.RemoveAll<DbContextOptions<InventoryDbContext>>();
            services.AddDbContext<InventoryDbContext>(options =>
            {
                options.UseSqlServer(databaseFixture.ConnectionString);
            });

            // Replace TimeProvider with FakeTimeProvider
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(TimeProvider);

            // Register test-only endpoint middleware for verifying DomainException mapping to 422 Problem Details
            services.AddSingleton<IStartupFilter>(new TestEndpointsStartupFilter());
        });
    }

    public InventoryDbContext CreateDbContext()
    {
        DbContextOptionsBuilder<InventoryDbContext> optionsBuilder =
            new DbContextOptionsBuilder<InventoryDbContext>().UseSqlServer(
                databaseFixture.ConnectionString
            );

        return new InventoryDbContext(optionsBuilder.Options);
    }

    private sealed class TestEndpointsStartupFilter : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
        {
            return app =>
            {
                next(app);

                app.Use(
                    async (context, nextMiddleware) =>
                    {
                        if (
                            context.Request.Path == "/test/throw-domain-exception"
                            && context.Request.Method == "POST"
                        )
                        {
                            throw new DomainException(
                                "Sample domain rule failure.",
                                "sample.domain_rule_violated"
                            );
                        }

                        await nextMiddleware();
                    }
                );
            };
        }
    }
}
