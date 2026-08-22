namespace DXOS.Infrastructure.Persistence.Entities;

public sealed class LeadRecord
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string Source { get; set; } = string.Empty;
    public string? SourcesJson { get; set; }
    public int Score { get; set; }
    public string Label { get; set; } = string.Empty;
    public string? ScoreBreakdownJson { get; set; }
    public string? ReasonsJson { get; set; }
    public Guid? CampaignId { get; set; }
    public string? AssignedToActor { get; set; }
    public DateTimeOffset? AssignedAtUtc { get; set; }
    public string? ClaimedByActor { get; set; }
    public DateTimeOffset? ClaimedAtUtc { get; set; }
    public string? RejectedByActorsJson { get; set; }
    public string? LastRejectionReason { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
}
