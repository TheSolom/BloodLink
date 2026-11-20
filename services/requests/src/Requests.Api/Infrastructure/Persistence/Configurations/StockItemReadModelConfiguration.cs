using BloodLink.Requests.Api.Infrastructure.Persistence.ReadModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BloodLink.Requests.Api.Infrastructure.Persistence.Configurations;

public class StockItemReadModelConfiguration : IEntityTypeConfiguration<StockItemReadModel>
{
    public void Configure(EntityTypeBuilder<StockItemReadModel> builder)
    {
        builder.ToTable("StockReadModel", "requests");

        builder.HasKey(s => s.UnitId);

        builder.Property(s => s.BloodType).HasMaxLength(10);
        builder.Property(s => s.Component).HasMaxLength(20);
        builder.Property(s => s.Status).IsRequired().HasMaxLength(20);
        builder.Property(s => s.Version).IsRequired();
        builder.Property(s => s.IsComplete).IsRequired();

        // Optimize candidate queries: match candidate is when IsComplete=true and Status='Available'
        builder.HasIndex(s => new { s.IsComplete, s.Status });
    }
}
