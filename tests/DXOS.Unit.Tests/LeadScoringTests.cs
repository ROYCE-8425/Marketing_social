using DXOS.Domain;
using Xunit;

namespace DXOS.Unit.Tests;

public sealed class LeadScoringTests
{
    private static readonly DateTimeOffset BusinessHourUtc = new(2026, 8, 21, 3, 0, 0, TimeSpan.Zero); // 10:00 AM VN (UTC+7)
    private static readonly DateTimeOffset OffHourUtc = new(2026, 8, 21, 15, 0, 0, TimeSpan.Zero); // 22:00 PM VN (UTC+7)

    [Fact]
    public void Score_5Factors_FullScore_Reaches100()
    {
        var campaignId = Guid.NewGuid();
        // Form (40) + Website (20) + Campaign (20) + Business hour (10) + High intent (10) = 100
        var (score, label, breakdown, reasons) = LeadScoring.Calculate(
            "Nguyen Van A muon dang ky hoc ngay",
            "0901234567",
            "a@example.com",
            LeadSource.Form,
            campaignId,
            BusinessHourUtc);

        Assert.Equal(100, score);
        Assert.Equal(LeadLabel.Hot, label);
        Assert.Equal(40, breakdown.Behavior);
        Assert.Equal(20, breakdown.Channel);
        Assert.Equal(20, breakdown.Campaign);
        Assert.Equal(10, breakdown.Time);
        Assert.Equal(10, breakdown.Intent);
        Assert.Equal(100, breakdown.Total);
        Assert.NotEmpty(reasons);
    }

    [Fact]
    public void Score_OffHoursAndNoCampaign_CalculatesCorrectly()
    {
        // Message (25) + stub-zalo (15) + no campaign (0) + off-hour (5) + standard intent (5) = 50
        var (score, label, breakdown, reasons) = LeadScoring.Calculate(
            "Khach Hang",
            "0901234567",
            null,
            LeadSource.Message,
            null,
            OffHourUtc);

        Assert.Equal(50, score);
        Assert.Equal(LeadLabel.Warm, label);
        Assert.Equal(25, breakdown.Behavior);
        Assert.Equal(15, breakdown.Channel);
        Assert.Equal(0, breakdown.Campaign);
        Assert.Equal(5, breakdown.Time);
        Assert.Equal(5, breakdown.Intent);
        Assert.Equal(50, breakdown.Total);
    }

    [Fact]
    public void Score_CallSource_CalculatesCorrectly()
    {
        // Call (30) + stub-call (15) + campaign (20) + business hour (10) + intent (10) = 85
        var (score, label, breakdown, _) = LeadScoring.Calculate(
            "Khach can bao gia gap",
            "0901234567",
            null,
            LeadSource.Call,
            Guid.NewGuid(),
            BusinessHourUtc);

        Assert.Equal(85, score);
        Assert.Equal(LeadLabel.Hot, label);
        Assert.Equal(30, breakdown.Behavior);
        Assert.Equal(15, breakdown.Channel);
        Assert.Equal(20, breakdown.Campaign);
    }

    [Fact]
    public void Labels_MapToCorrectScoreRanges()
    {
        Assert.Equal(LeadLabel.Hot, GetLabel(80));
        Assert.Equal(LeadLabel.Hot, GetLabel(100));
        Assert.Equal(LeadLabel.Warm, GetLabel(50));
        Assert.Equal(LeadLabel.Warm, GetLabel(79));
        Assert.Equal(LeadLabel.Cold, GetLabel(20));
        Assert.Equal(LeadLabel.Cold, GetLabel(49));
        Assert.Equal(LeadLabel.Junk, GetLabel(19));
        Assert.Equal(LeadLabel.Junk, GetLabel(0));
    }

    private static LeadLabel GetLabel(int score) => score switch
    {
        >= 80 => LeadLabel.Hot,
        >= 50 => LeadLabel.Warm,
        >= 20 => LeadLabel.Cold,
        _ => LeadLabel.Junk
    };
}
