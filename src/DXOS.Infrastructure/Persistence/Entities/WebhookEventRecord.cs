namespace DXOS.Infrastructure.Persistence.Entities;

public sealed class WebhookEventRecord
{
    public Guid Id { get; set; }
    public string Provider { get; set; } = string.Empty;
    public string ExternalEventId { get; set; } = string.Empty;
    public string PayloadHash { get; set; } = string.Empty;
    public string Status { get; set; } = "received";
    public Guid? LeadId { get; set; }
    public DateTimeOffset ReceivedAtUtc { get; set; }
    public DateTimeOffset? ProcessedAtUtc { get; set; }
}
