using BloodLink.Inventory.Domain.Model.BloodBanks;
using BloodLink.Inventory.Domain.Model.BloodUnits;
using BloodLink.Inventory.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BloodLink.Inventory.Api.Infrastructure.Persistence.Configurations;

public class BloodUnitConfiguration : IEntityTypeConfiguration<BloodUnit>
{
    public void Configure(EntityTypeBuilder<BloodUnit> builder)
    {
        builder.ToTable("BloodUnits", "inventory");

        builder.HasKey(u => u.Id);

        builder.Property(u => u.DonationNumber)
            .HasConversion(
                d => d.Value,
                value => DonationNumber.Create(value)
            )
            .IsRequired()
            .HasMaxLength(32);

        builder.Property(u => u.BloodType)
            .HasConversion(
                bt => bt.ToString(),
                value => BloodType.Parse(value)
            )
            .IsRequired()
            .HasMaxLength(10);

        builder.Property(u => u.Component)
            .HasConversion<string>()
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(u => u.Status)
            .HasConversion<string>()
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(u => u.CollectedAt)
            .IsRequired();

        builder.Property(u => u.ExpiresAt)
            .IsRequired();

        builder.Property(u => u.Version)
            .IsConcurrencyToken();

        builder.Property(u => u.CurrentReservationRequestId);

        builder.HasOne<BloodBank>()
            .WithMany()
            .HasForeignKey(u => u.BloodBankId)
            .OnDelete(DeleteBehavior.Restrict);

        // Unique index per blood bank on DonationNumber
        builder.HasIndex(u => new { u.BloodBankId, u.DonationNumber })
            .IsUnique();

        // Composite index optimizing available stock queries
        builder.HasIndex(u => new { u.Status, u.BloodType, u.Component, u.ExpiresAt });
    }
}
