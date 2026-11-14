using BloodLink.BuildingBlocks.Messaging;
using BloodLink.Requests.Api.Infrastructure.Persistence.ReadModels;
using BloodLink.Requests.Domain.BloodRequests;
using BloodLink.Requests.Domain.Hospitals;
using Microsoft.EntityFrameworkCore;

namespace BloodLink.Requests.Api.Infrastructure.Persistence;

public class RequestsDbContext(DbContextOptions<RequestsDbContext> options) : DbContext(options)
{
    public DbSet<BloodRequest> BloodRequests => Set<BloodRequest>();
    public DbSet<Hospital> Hospitals => Set<Hospital>();
    public DbSet<StockItemReadModel> StockReadModels => Set<StockItemReadModel>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("requests");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(RequestsDbContext).Assembly);
        modelBuilder.ConfigureTransactionalOutbox("requests");
        base.OnModelCreating(modelBuilder);
    }
}
