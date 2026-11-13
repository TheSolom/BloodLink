using System.Data.Common;
using BloodLink.Inventory.Api.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Respawn;
using Testcontainers.MsSql;
using Testcontainers.RabbitMq;
using Xunit;

namespace BloodLink.Inventory.IntegrationTests.Fixtures;

public class DatabaseFixture : IAsyncLifetime
{
    private readonly MsSqlContainer _msSqlContainer = new MsSqlBuilder(
        "mcr.microsoft.com/mssql/server:2025-CU10-ubuntu-24.04"
    ).Build();

    private readonly RabbitMqContainer _rabbitMqContainer = new RabbitMqBuilder(
        "masstransit/rabbitmq:4.3.1"
    ).Build();

    private DbConnection _dbConnection = null!;
    private Respawner _respawner = null!;

    public string ConnectionString => _msSqlContainer.GetConnectionString();
    public string RabbitMqConnectionString => _rabbitMqContainer.GetConnectionString();
    private Uri RabbitMqUri => new(_rabbitMqContainer.GetConnectionString());
    public string RabbitMqHost => RabbitMqUri.Host;
    public ushort RabbitMqPort => (ushort)RabbitMqUri.Port;
    public string RabbitMqUsername => RabbitMqUri.UserInfo.Split(':')[0];
    public string RabbitMqPassword => RabbitMqUri.UserInfo.Split(':')[1];

    public async Task InitializeAsync()
    {
        await Task.WhenAll(_msSqlContainer.StartAsync(), _rabbitMqContainer.StartAsync());

        _dbConnection = new SqlConnection(ConnectionString);
        await _dbConnection.OpenAsync();

        // Run migrations against test container
        DbContextOptionsBuilder<InventoryDbContext> optionsBuilder =
            new DbContextOptionsBuilder<InventoryDbContext>().UseSqlServer(ConnectionString);

        await using var dbContext = new InventoryDbContext(optionsBuilder.Options);
        await dbContext.Database.MigrateAsync();

        // Configure Respawn to reset only the inventory schema and preserve EF migrations history
        _respawner = await Respawner.CreateAsync(
            _dbConnection,
            new RespawnerOptions
            {
                SchemasToInclude = ["inventory"],
                TablesToIgnore = ["__EFMigrationsHistory"],
            }
        );
    }

    public async Task ResetDatabaseAsync() => await _respawner.ResetAsync(_dbConnection);

    public async Task DisposeAsync()
    {
        if (_dbConnection is not null)
        {
            await _dbConnection.DisposeAsync();
        }

        await Task.WhenAll(
            _msSqlContainer.DisposeAsync().AsTask(),
            _rabbitMqContainer.DisposeAsync().AsTask()
        );
    }
}
