namespace DXOS.Infrastructure.Persistence.Entities;

public sealed class SpendProposalRecord
{
    public Guid Id { get; set; }
    public string FromNote { get; set; } = string.Empty;
    public string ToNote { get; set; } = string.Empty;
    public decimal Percent { get; set; }
    public string Rationale { get; set; } = string.Empty;
    public string ProposedByRole { get; set; } = string.Empty;
    public string ProposedByActor { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? RejectionReason { get; set; }
    public string? DecidedByActor { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset? DecidedAtUtc { get; set; }
}
