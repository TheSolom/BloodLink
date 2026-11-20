using BloodLink.Requests.Domain.Hospitals;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BloodLink.Requests.Api.Infrastructure.Persistence.Configurations;

public class HospitalConfiguration : IEntityTypeConfiguration<Hospital>
{
    public void Configure(EntityTypeBuilder<Hospital> builder)
    {
        builder.ToTable("Hospitals", "requests");

        builder.HasKey(h => h.Id);

        builder.Property(h => h.Name).IsRequired().HasMaxLength(200);

        builder.OwnsOne(
            h => h.Location,
            loc =>
            {
                loc.Property(l => l.Latitude).HasColumnName("Latitude").IsRequired();
                loc.Property(l => l.Longitude).HasColumnName("Longitude").IsRequired();
            }
        );

        builder.HasData(
            new { Id = SeededHospitals.StThomasId, Name = "St. Thomas' Hospital" },
            new { Id = SeededHospitals.RoyalLondonId, Name = "Royal London Hospital" },
            new { Id = SeededHospitals.KingsCollegeId, Name = "King's College Hospital" }
        );

        builder
            .OwnsOne(h => h.Location)
            .HasData(
                new
                {
                    HospitalId = SeededHospitals.StThomasId,
                    Latitude = 51.4988,
                    Longitude = -0.1186,
                },
                new
                {
                    HospitalId = SeededHospitals.RoyalLondonId,
                    Latitude = 51.5190,
                    Longitude = -0.0595,
                },
                new
                {
                    HospitalId = SeededHospitals.KingsCollegeId,
                    Latitude = 51.4682,
                    Longitude = -0.0934,
                }
            );
    }
}
