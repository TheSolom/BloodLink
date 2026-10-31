using System.Data.Common;
using BloodLink.Inventory.Api.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Respawn;
using Testcontainers.MsSql;
using Xunit;

namespace BloodLink.Inventory.IntegrationTests.Fixtures;

public class DatabaseFixture : IAsyncLifetime
{
    private readonly MsSqlContainer _msSqlContainer = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2025-CU10-ubuntu-24.04")
        .Build();

    private DbConnection _dbConnection = null!;
    private Respawner _respawner = null!;

    public string ConnectionString => _msSqlContainer.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _msSqlContainer.StartAsync();

        _dbConnection = new SqlConnection(ConnectionString);
        await _dbConnection.OpenAsync();

        // Run migrations against test container
        var optionsBuilder = new DbContextOptionsBuilder<InventoryDbContext>()
            .UseSqlServer(ConnectionString);

        await using var dbContext = new InventoryDbContext(optionsBuilder.Options);
        await dbContext.Database.MigrateAsync();

        // Configure Respawn to reset only the inventory schema and preserve EF migrations history
        _respawner = await Respawner.CreateAsync(_dbConnection, new RespawnerOptions
        {
            SchemasToInclude = ["inventory"],
            TablesToIgnore = ["__EFMigrationsHistory"]
        });
    }

    public async Task ResetDatabaseAsync()
    {
        await _respawner.ResetAsync(_dbConnection);
    }

    public async Task DisposeAsync()
    {
        if (_dbConnection is not null)
        {
            await _dbConnection.DisposeAsync();
        }

        await _msSqlContainer.DisposeAsync();
    }
}

