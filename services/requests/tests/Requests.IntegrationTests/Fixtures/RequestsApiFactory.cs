using System.Globalization;
using BloodLink.Requests.Api.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace BloodLink.Requests.IntegrationTests.Fixtures;

public class RequestsApiFactory(DatabaseFixture databaseFixture) : WebApplicationFactory<Program>
{
    public FakeTimeProvider TimeProvider { get; } =
        new(new DateTimeOffset(2026, 11, 15, 12, 0, 0, TimeSpan.Zero));

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting(
            "ConnectionStrings:RequestsConnection",
            databaseFixture.ConnectionString
        );
        builder.UseSetting("RabbitMq:Host", databaseFixture.RabbitMqHost);
        builder.UseSetting(
            "RabbitMq:Port",
            databaseFixture.RabbitMqPort.ToString(CultureInfo.InvariantCulture)
        );
        builder.UseSetting("RabbitMq:UserName", databaseFixture.RabbitMqUsername);
        builder.UseSetting("RabbitMq:Password", databaseFixture.RabbitMqPassword);
        builder.UseSetting("RabbitMq:VirtualHost", "/");

        builder.ConfigureTestServices(services =>
        {
            // Replace DbContext configuration with the container connection string
            services.RemoveAll<DbContextOptions<RequestsDbContext>>();
            services.AddDbContext<RequestsDbContext>(options =>
            {
                options.UseSqlServer(databaseFixture.ConnectionString);
            });

            // Replace TimeProvider with FakeTimeProvider
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(TimeProvider);
        });
    }

    public RequestsDbContext CreateDbContext()
    {
        DbContextOptionsBuilder<RequestsDbContext> optionsBuilder =
            new DbContextOptionsBuilder<RequestsDbContext>().UseSqlServer(
                databaseFixture.ConnectionString
            );

        return new RequestsDbContext(optionsBuilder.Options);
    }
}
