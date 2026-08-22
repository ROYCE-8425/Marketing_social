namespace DXOS.Application;

public interface IWebhookEventStore
{
    Task<WebhookBeginResult> TryBeginAsync(
        string provider,
        string externalEventId,
        string payloadHash,
        CancellationToken cancellationToken);

    Task CompleteAsync(Guid webhookEventId, Guid leadId, CancellationToken cancellationToken);

    Task<DateTimeOffset?> GetLastReceivedAtUtcAsync(CancellationToken cancellationToken);
}

public sealed record WebhookBeginResult(bool IsDuplicate, Guid WebhookEventId, Guid? ExistingLeadId);
