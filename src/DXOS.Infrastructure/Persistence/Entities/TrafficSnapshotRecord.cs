namespace DXOS.Infrastructure.Persistence.Entities;

public sealed class TrafficSnapshotRecord
{
    public Guid Id { get; set; }
    public Guid CampaignId { get; set; }
    public DateOnly PeriodDate { get; set; }
    public long Impressions { get; set; }
    public long Clicks { get; set; }
    public long Visits { get; set; }
    public decimal SpendVnd { get; set; }
    public string Source { get; set; } = "Manual";
    public string RecordedByActor { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; }
}
