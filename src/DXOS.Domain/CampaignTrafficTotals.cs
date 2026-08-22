namespace DXOS.Domain;

public sealed record CampaignTrafficTotals(
    long Impressions,
    long Clicks,
    long Visits,
    decimal SpendVnd,
    decimal Ctr)
{
    public static CampaignTrafficTotals Empty => new(0, 0, 0, 0, 0);

    public static CampaignTrafficTotals Aggregate(IEnumerable<TrafficSnapshot> snapshots)
    {
        long impressions = 0;
        long clicks = 0;
        long visits = 0;
        decimal spend = 0;

        foreach (var s in snapshots)
        {
            impressions += s.Impressions;
            clicks += s.Clicks;
            visits += s.Visits;
            spend += s.SpendVnd;
        }

        var ctr = impressions == 0
            ? 0
            : decimal.Round((decimal)clicks / impressions, 4, MidpointRounding.AwayFromZero);

        return new CampaignTrafficTotals(impressions, clicks, visits, spend, ctr);
    }
}
