using DXOS.Application;
using DXOS.Domain;
using DXOS.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace DXOS.Infrastructure.Persistence;

public sealed class TrafficStore : ITrafficStore
{
    private readonly BootstrapDbContext _db;

    public TrafficStore(BootstrapDbContext db)
    {
        _db = db;
    }

    public async Task AddSnapshotAsync(TrafficSnapshot snapshot, CancellationToken cancellationToken)
    {
        _db.TrafficSnapshots.Add(ToRecord(snapshot));
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<TrafficSnapshot>> ListByCampaignAsync(Guid campaignId, CancellationToken cancellationToken)
    {
        var records = await _db.TrafficSnapshots
            .AsNoTracking()
            .Where(t => t.CampaignId == campaignId)
            .OrderBy(t => t.PeriodDate)
            .ThenBy(t => t.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        return records.Select(ToDomain).ToList();
    }

    public async Task<IReadOnlyList<TrafficSnapshot>> ListAllAsync(CancellationToken cancellationToken)
    {
        var records = await _db.TrafficSnapshots
            .AsNoTracking()
            .OrderBy(t => t.PeriodDate)
            .ThenBy(t => t.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        return records.Select(ToDomain).ToList();
    }

    public async Task<decimal> GetTotalSpendVndAsync(CancellationToken cancellationToken)
    {
        return await _db.TrafficSnapshots
            .AsNoTracking()
            .SumAsync(t => t.SpendVnd, cancellationToken);
    }

    public async Task<decimal> GetCampaignTotalSpendVndAsync(Guid campaignId, CancellationToken cancellationToken)
    {
        return await _db.TrafficSnapshots
            .AsNoTracking()
            .Where(t => t.CampaignId == campaignId)
            .SumAsync(t => t.SpendVnd, cancellationToken);
    }

    private static TrafficSnapshotRecord ToRecord(TrafficSnapshot domain)
    {
        return new TrafficSnapshotRecord
        {
            Id = domain.Id,
            CampaignId = domain.CampaignId,
            PeriodDate = domain.PeriodDate,
            Impressions = domain.Impressions,
            Clicks = domain.Clicks,
            Visits = domain.Visits,
            SpendVnd = domain.SpendVnd,
            Source = domain.Source.ToString(),
            RecordedByActor = domain.RecordedByActor,
            CreatedAtUtc = domain.CreatedAtUtc
        };
    }

    private static TrafficSnapshot ToDomain(TrafficSnapshotRecord record)
    {
        var source = Enum.TryParse<TrafficSource>(record.Source, ignoreCase: true, out var parsed)
            ? parsed
            : TrafficSource.Manual;

        return TrafficSnapshot.Restore(
            record.Id,
            record.CampaignId,
            record.PeriodDate,
            record.Impressions,
            record.Clicks,
            record.Visits,
            record.SpendVnd,
            source,
            record.RecordedByActor,
            record.CreatedAtUtc);
    }
}
