using DXOS.Application;
using DXOS.Domain;
using DXOS.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace DXOS.Infrastructure.Persistence;

public sealed class CampaignStore : ICampaignStore
{
    private readonly BootstrapDbContext _db;

    public CampaignStore(BootstrapDbContext db)
    {
        _db = db;
    }

    public async Task AddAsync(Campaign campaign, CancellationToken cancellationToken)
    {
        _db.Campaigns.Add(ToRecord(campaign));
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<Campaign?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var record = await _db.Campaigns.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        return record is null ? null : ToDomain(record);
    }

    public async Task<IReadOnlyList<Campaign>> ListAsync(CancellationToken cancellationToken)
    {
        var records = await _db.Campaigns
            .AsNoTracking()
            .OrderByDescending(c => c.CreatedAtUtc)
            .ToListAsync(cancellationToken);
        return records.Select(ToDomain).ToList();
    }

    public async Task UpdateAsync(Campaign campaign, CancellationToken cancellationToken)
    {
        var record = await _db.Campaigns.FirstOrDefaultAsync(c => c.Id == campaign.Id, cancellationToken)
            ?? throw new InvalidOperationException($"Campaign '{campaign.Id}' was not found.");
        record.Topic = campaign.Topic;
        record.Copy = campaign.Copy;
        record.CopySnapshot = campaign.CopySnapshot;
        record.Status = campaign.Status.ToString();
        record.RejectionReason = campaign.RejectionReason;
        record.ApprovedAtUtc = campaign.ApprovedAtUtc;
        record.CreatedByActor = campaign.CreatedByActor;
        record.CreatedAtUtc = campaign.CreatedAtUtc;
        record.UpdatedAtUtc = campaign.UpdatedAtUtc;
        await _db.SaveChangesAsync(cancellationToken);
    }

    private static CampaignRecord ToRecord(Campaign campaign)
    {
        return new CampaignRecord
        {
            Id = campaign.Id,
            Topic = campaign.Topic,
            Copy = campaign.Copy,
            CopySnapshot = campaign.CopySnapshot,
            Status = campaign.Status.ToString(),
            RejectionReason = campaign.RejectionReason,
            ApprovedAtUtc = campaign.ApprovedAtUtc,
            CreatedByActor = campaign.CreatedByActor,
            CreatedAtUtc = campaign.CreatedAtUtc,
            UpdatedAtUtc = campaign.UpdatedAtUtc
        };
    }

    private static Campaign ToDomain(CampaignRecord record)
    {
        return Campaign.Restore(
            record.Id,
            record.Topic,
            record.Copy,
            record.CopySnapshot,
            Enum.Parse<CampaignStatus>(record.Status),
            record.RejectionReason,
            record.ApprovedAtUtc,
            record.CreatedByActor,
            record.CreatedAtUtc,
            record.UpdatedAtUtc);
    }
}
