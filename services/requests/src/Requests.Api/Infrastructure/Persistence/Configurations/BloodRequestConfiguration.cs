using System.Text.Json;
using BloodLink.Requests.Domain.BloodRequests;
using BloodLink.Requests.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BloodLink.Requests.Api.Infrastructure.Persistence.Configurations;

public class BloodRequestConfiguration : IEntityTypeConfiguration<BloodRequest>
{
    public void Configure(EntityTypeBuilder<BloodRequest> builder)
    {
        builder.ToTable("BloodRequests", "requests");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.HospitalId).IsRequired();

        builder.OwnsOne(
            r => r.HospitalLocation,
            loc =>
            {
                loc.Property(l => l.Latitude).HasColumnName("HospitalLatitude").IsRequired();
                loc.Property(l => l.Longitude).HasColumnName("HospitalLongitude").IsRequired();
            }
        );

        builder
            .Property(r => r.BloodType)
            .HasConversion(bt => bt.ToString(), value => BloodType.Parse(value))
            .IsRequired()
            .HasMaxLength(10);

        builder.Property(r => r.Component).HasConversion<string>().IsRequired().HasMaxLength(20);

        builder.Property(r => r.UnitsNeeded).IsRequired();

        builder.Property(r => r.Urgency).HasConversion<string>().IsRequired().HasMaxLength(20);

        builder.Property(r => r.NeededBy).IsRequired();

        builder.Property(r => r.Status).HasConversion<string>().IsRequired().HasMaxLength(20);

        builder.Property(r => r.IdempotencyKey).IsRequired().HasMaxLength(128);

        // Unique index per hospital on IdempotencyKey (prevents duplicate submission)
        builder.HasIndex(r => new { r.HospitalId, r.IdempotencyKey }).IsUnique();

        // Storing assigned unit ids as JSON array
        var valueComparer = new ValueComparer<IReadOnlyList<Guid>>(
            (c1, c2) =>
                (c1 == null && c2 == null) || (c1 != null && c2 != null && c1.SequenceEqual(c2)),
            c => c.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
            c => c.ToList()
        );

        builder
            .Property(r => r.AssignedUnitIds)
            .HasField("_assignedUnitIds")
            .HasConversion(
                ids => JsonSerializer.Serialize(ids, (JsonSerializerOptions?)null),
                json =>
                    string.IsNullOrEmpty(json)
                        ? new List<Guid>()
                        : (
                            JsonSerializer.Deserialize<List<Guid>>(
                                json,
                                (JsonSerializerOptions?)null
                            ) ?? new List<Guid>()
                        )
            )
            .Metadata.SetValueComparer(valueComparer);
    }
}
