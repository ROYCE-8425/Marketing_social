using DXOS.Domain;
using Xunit;

namespace DXOS.Unit.Tests;

public sealed class TrafficSnapshotTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 21, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Today = new(2026, 8, 21);

    [Fact]
    public void Create_ValidInputs_InitializesSnapshot()
    {
        var campaignId = Guid.NewGuid();
        var snapshot = TrafficSnapshot.Create(
            campaignId,
            Today,
            impressions: 1000,
            clicks: 50,
            visits: 45,
            spendVnd: 2_500_000.4m,
            recordedByActor: "mai",
            nowUtc: Now);

        Assert.Equal(campaignId, snapshot.CampaignId);
        Assert.Equal(Today, snapshot.PeriodDate);
        Assert.Equal(1000, snapshot.Impressions);
        Assert.Equal(50, snapshot.Clicks);
        Assert.Equal(45, snapshot.Visits);
        Assert.Equal(2_500_000m, snapshot.SpendVnd);
        Assert.Equal(TrafficSource.Manual, snapshot.Source);
        Assert.Equal("mai", snapshot.RecordedByActor);
        Assert.Equal(Now, snapshot.CreatedAtUtc);
    }

    [Fact]
    public void Create_NegativeMetrics_ThrowsDomainRuleException()
    {
        var campaignId = Guid.NewGuid();
        Assert.Throws<DomainRuleException>(() => TrafficSnapshot.Create(campaignId, Today, -1, 0, 0, 0, "mai", Now));
        Assert.Throws<DomainRuleException>(() => TrafficSnapshot.Create(campaignId, Today, 100, -1, 0, 0, "mai", Now));
        Assert.Throws<DomainRuleException>(() => TrafficSnapshot.Create(campaignId, Today, 100, 10, -1, 0, "mai", Now));
        Assert.Throws<DomainRuleException>(() => TrafficSnapshot.Create(campaignId, Today, 100, 10, 5, -1, "mai", Now));
    }

    [Fact]
    public void Create_ClicksExceedImpressions_ThrowsDomainRuleException()
    {
        var campaignId = Guid.NewGuid();
        var ex = Assert.Throws<DomainRuleException>(() =>
            TrafficSnapshot.Create(campaignId, Today, 100, 101, 50, 500_000, "mai", Now));
        Assert.Equal("InvalidMetric", ex.Code);
    }

    [Fact]
    public void Create_MissingActor_ThrowsDomainRuleException()
    {
        var campaignId = Guid.NewGuid();
        var ex = Assert.Throws<DomainRuleException>(() =>
            TrafficSnapshot.Create(campaignId, Today, 100, 10, 5, 500_000, " ", Now));
        Assert.Equal("InvalidActor", ex.Code);
    }

    [Fact]
    public void Aggregate_CalculatesTotalsAndCtr()
    {
        var campaignId = Guid.NewGuid();
        var s1 = TrafficSnapshot.Create(campaignId, Today, 1000, 50, 40, 1_000_000, "mai", Now);
        var s2 = TrafficSnapshot.Create(campaignId, Today.AddDays(1), 2000, 150, 120, 2_000_000, "mai", Now);

        var totals = CampaignTrafficTotals.Aggregate([s1, s2]);

        Assert.Equal(3000, totals.Impressions);
        Assert.Equal(200, totals.Clicks);
        Assert.Equal(160, totals.Visits);
        Assert.Equal(3_000_000m, totals.SpendVnd);
        Assert.Equal(0.0667m, totals.Ctr);
    }

    [Fact]
    public void Aggregate_ZeroImpressions_CtrIsZero()
    {
        var totals = CampaignTrafficTotals.Aggregate([]);
        Assert.Equal(0, totals.Impressions);
        Assert.Equal(0, totals.Ctr);
    }
}
