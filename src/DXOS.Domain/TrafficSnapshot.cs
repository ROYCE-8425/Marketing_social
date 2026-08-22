namespace DXOS.Domain;

public sealed class TrafficSnapshot
{
    private TrafficSnapshot()
    {
    }

    public Guid Id { get; private set; }
    public Guid CampaignId { get; private set; }
    public DateOnly PeriodDate { get; private set; }
    public long Impressions { get; private set; }
    public long Clicks { get; private set; }
    public long Visits { get; private set; }
    public decimal SpendVnd { get; private set; }
    public TrafficSource Source { get; private set; }
    public string RecordedByActor { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; private set; }

    public static TrafficSnapshot Create(
        Guid campaignId,
        DateOnly periodDate,
        long impressions,
        long clicks,
        long visits,
        decimal spendVnd,
        string recordedByActor,
        DateTimeOffset nowUtc)
    {
        if (campaignId == Guid.Empty)
        {
            throw new DomainRuleException("InvalidCampaign", "Campaign ID is required.");
        }

        if (impressions < 0)
        {
            throw new DomainRuleException("InvalidMetric", "Impressions must be non-negative.");
        }

        if (clicks < 0)
        {
            throw new DomainRuleException("InvalidMetric", "Clicks must be non-negative.");
        }

        if (clicks > impressions)
        {
            throw new DomainRuleException("InvalidMetric", "Clicks cannot exceed impressions.");
        }

        if (visits < 0)
        {
            throw new DomainRuleException("InvalidMetric", "Visits must be non-negative.");
        }

        if (spendVnd < 0)
        {
            throw new DomainRuleException("InvalidMetric", "Spend must be non-negative.");
        }

        if (string.IsNullOrWhiteSpace(recordedByActor))
        {
            throw new DomainRuleException("InvalidActor", "Recorded by actor is required.");
        }

        var safeSpend = decimal.Round(spendVnd, 0, MidpointRounding.AwayFromZero);

        return new TrafficSnapshot
        {
            Id = Guid.NewGuid(),
            CampaignId = campaignId,
            PeriodDate = periodDate,
            Impressions = impressions,
            Clicks = clicks,
            Visits = visits,
            SpendVnd = safeSpend,
            Source = TrafficSource.Manual,
            RecordedByActor = recordedByActor.Trim(),
            CreatedAtUtc = nowUtc
        };
    }

    public static TrafficSnapshot Restore(
        Guid id,
        Guid campaignId,
        DateOnly periodDate,
        long impressions,
        long clicks,
        long visits,
        decimal spendVnd,
        TrafficSource source,
        string recordedByActor,
        DateTimeOffset createdAtUtc)
    {
        return new TrafficSnapshot
        {
            Id = id,
            CampaignId = campaignId,
            PeriodDate = periodDate,
            Impressions = impressions,
            Clicks = clicks,
            Visits = visits,
            SpendVnd = spendVnd,
            Source = source,
            RecordedByActor = recordedByActor,
            CreatedAtUtc = createdAtUtc
        };
    }
}

public enum TrafficSource
{
    Manual = 0,
    AdsConnector = 1
}
