using DXOS.Application;
using DXOS.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace DXOS.Infrastructure.Persistence;

public sealed class WebhookEventStore : IWebhookEventStore
{
    private readonly BootstrapDbContext _db;

    public WebhookEventStore(BootstrapDbContext db)
    {
        _db = db;
    }

    public async Task<WebhookBeginResult> TryBeginAsync(
        string provider,
        string externalEventId,
        string payloadHash,
        CancellationToken cancellationToken)
    {
        var existing = await _db.WebhookEvents
            .AsNoTracking()
            .FirstOrDefaultAsync(
                e => e.Provider == provider && e.ExternalEventId == externalEventId,
                cancellationToken);

        if (existing is not null)
        {
            return new WebhookBeginResult(true, existing.Id, existing.LeadId);
        }

        var record = new WebhookEventRecord
        {
            Id = Guid.NewGuid(),
            Provider = provider,
            ExternalEventId = externalEventId,
            PayloadHash = payloadHash,
            Status = "received",
            ReceivedAtUtc = DateTimeOffset.UtcNow
        };

        _db.WebhookEvents.Add(record);

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            var raced = await _db.WebhookEvents
                .AsNoTracking()
                .FirstAsync(
                    e => e.Provider == provider && e.ExternalEventId == externalEventId,
                    cancellationToken);
            return new WebhookBeginResult(true, raced.Id, raced.LeadId);
        }

        return new WebhookBeginResult(false, record.Id, null);
    }

    public async Task CompleteAsync(Guid webhookEventId, Guid leadId, CancellationToken cancellationToken)
    {
        var record = await _db.WebhookEvents.FirstOrDefaultAsync(e => e.Id == webhookEventId, cancellationToken)
            ?? throw new InvalidOperationException($"Webhook event '{webhookEventId}' was not found.");
        record.LeadId = leadId;
        record.Status = "processed";
        record.ProcessedAtUtc = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<DateTimeOffset?> GetLastReceivedAtUtcAsync(CancellationToken cancellationToken)
    {
        return await _db.WebhookEvents
            .AsNoTracking()
            .OrderByDescending(e => e.ReceivedAtUtc)
            .Select(e => (DateTimeOffset?)e.ReceivedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
