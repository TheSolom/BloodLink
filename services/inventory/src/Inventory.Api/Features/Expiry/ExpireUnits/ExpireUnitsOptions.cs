namespace BloodLink.Inventory.Api.Features.Expiry.ExpireUnits;

public class ExpireUnitsOptions
{
    public const string SectionName = "Inventory:Expiry";

    public TimeSpan SweepInterval { get; set; } = TimeSpan.FromMinutes(1);

    public int BatchSize { get; set; } = 50;

    public bool Enabled { get; set; } = true;
}
