using BloodLink.Inventory.Domain.Model.BloodBanks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BloodLink.Inventory.Api.Infrastructure.Persistence.Configurations;

public class BloodBankConfiguration : IEntityTypeConfiguration<BloodBank>
{
    public void Configure(EntityTypeBuilder<BloodBank> builder)
    {
        builder.ToTable("BloodBanks", "inventory");

        builder.HasKey(b => b.Id);

        builder.Property(b => b.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.OwnsOne(b => b.Location, loc =>
        {
            loc.Property(l => l.Latitude)
                .HasColumnName("Latitude")
                .IsRequired();

            loc.Property(l => l.Longitude)
                .HasColumnName("Longitude")
                .IsRequired();
        });
    }
}
