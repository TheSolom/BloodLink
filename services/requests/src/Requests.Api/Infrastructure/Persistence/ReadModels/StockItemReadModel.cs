namespace BloodLink.Requests.Api.Infrastructure.Persistence.ReadModels;

public class StockItemReadModel
{
    public Guid UnitId { get; set; }
    public Guid? BloodBankId { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public string? BloodType { get; set; }
    public string? Component { get; set; }
    public DateTimeOffset? ExpiresAt { get; set; }
    public string Status { get; set; } = string.Empty;
    public int Version { get; set; }
    public bool IsComplete { get; set; }

    public bool IsMatchCandidate =>
        IsComplete && string.Equals(Status, "Available", StringComparison.OrdinalIgnoreCase);
}
