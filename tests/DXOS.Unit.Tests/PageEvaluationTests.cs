using DXOS.Domain;
using Xunit;

namespace DXOS.Unit.Tests;

public sealed class PageEvaluationTests
{
    [Fact]
    public void CompletenessBelowHalf_CannotBeHealthy_CapsAtWatchOrCritical()
    {
        var now = DateTimeOffset.UtcNow;
        var evidence = new PageEvaluationEvidence(
            PageId: "988656934325292",
            PageName: "Royce Shop",
            FanCount: 100,
            FollowersCount: 100,
            // 1 active post within 1 day (Content score will be high: ~100)
            Posts: new[]
            {
                new PagePostEvidence(
                    PostId: "p1",
                    Message: "Flash Sale Royce Shop inbox ngay để nhận ưu đãi!",
                    CreatedTimeUtc: now.AddHours(-12),
                    ReactionCount: 0,
                    CommentCount: 0,
                    ShareCount: 0,
                    Impressions: 0,
                    EngagedUsers: 0,
                    Clicks: 0,
                    DataFreshness: "none" // unmeasured engagement
                )
            },
            // No inbox messages
            TotalConversations: 0,
            UnrepliedConversations: 0,
            ConversationsWithPhone: 0,
            // No leads
            TotalLeads: 0,
            HotLeads: 0,
            WarmLeads: 0,
            CommentsPermissionForbidden: true,
            InsightsPartialOrForbidden: true
        );

        var result = PageEvaluation.Evaluate(evidence, now);

        // Only Content axis is measurable (1 out of 4 -> completeness = 0.25)
        Assert.Equal(0.25, result.Axes.Completeness);
        Assert.NotNull(result.Axes.Content);
        Assert.Null(result.Axes.Inbox);
        Assert.Null(result.Axes.Leads);
        Assert.Null(result.Axes.Engagement);

        // Even if Content is high, label CANNOT be Healthy because completeness < 0.5
        Assert.NotEqual(PageHealthLabel.Healthy, result.Label);
        Assert.Contains(result.Reasons, r => r.Contains("Độ đầy đủ"));
    }

    [Fact]
    public void AllFourAxesAvailable_HighPerformance_ReturnsHealthy()
    {
        var now = DateTimeOffset.UtcNow;
        var evidence = new PageEvaluationEvidence(
            PageId: "988656934325292",
            PageName: "Royce Shop",
            FanCount: 500,
            FollowersCount: 600,
            // 5 active posts within 7 days with high engagement
            Posts: new[]
            {
                new PagePostEvidence("p1", "Áo thun mới hotline 0901234567 liên hệ ngay", now.AddDays(-1), 50, 10, 5, 2000, 300, 100, "fresh"),
                new PagePostEvidence("p2", "BST mới inbox để tư vấn size chuẩn", now.AddDays(-2), 40, 8, 4, 1800, 250, 80, "fresh"),
                new PagePostEvidence("p3", "Khuyến mãi đặt hàng tại link hoặc sđt", now.AddDays(-4), 30, 5, 2, 1500, 200, 60, "fresh"),
                new PagePostEvidence("p4", "Feedback khách hàng mua hàng nhắn tin", now.AddDays(-5), 25, 4, 1, 1200, 150, 40, "fresh"),
                new PagePostEvidence("p5", "Bộ sưu tập mùa đông liên hệ hotline", now.AddDays(-6), 20, 3, 1, 1000, 120, 30, "fresh")
            },
            // Fast response inbox
            TotalConversations: 20,
            UnrepliedConversations: 1, // 5% unreplied
            ConversationsWithPhone: 12, // 60% with phone
                                        // Healthy leads
            TotalLeads: 25,
            HotLeads: 10,
            WarmLeads: 8,
            CommentsPermissionForbidden: false,
            InsightsPartialOrForbidden: false
        );

        var result = PageEvaluation.Evaluate(evidence, now);

        Assert.Equal(1.0, result.Axes.Completeness);
        Assert.NotNull(result.Axes.Content);
        Assert.NotNull(result.Axes.Inbox);
        Assert.NotNull(result.Axes.Leads);
        Assert.NotNull(result.Axes.Engagement);
        Assert.True(result.OverallScore >= 75);
        Assert.Equal(PageHealthLabel.Healthy, result.Label);
    }

    [Fact]
    public void DeterministicOutput_IdenticalInputsYieldIdenticalResults()
    {
        var now = new DateTimeOffset(2026, 8, 24, 12, 0, 0, TimeSpan.Zero);
        var evidence1 = new PageEvaluationEvidence(
            PageId: "page_abc",
            PageName: "ABC Shop",
            FanCount: 200,
            FollowersCount: 200,
            Posts: new[]
            {
                new PagePostEvidence("p1", "Sale sđt 0909999999 inbox shop", now.AddDays(-2), 10, 2, 1, 500, 50, 20, "fresh")
            },
            TotalConversations: 10,
            UnrepliedConversations: 1,
            ConversationsWithPhone: 5,
            TotalLeads: 5,
            HotLeads: 2,
            WarmLeads: 2,
            CommentsPermissionForbidden: false,
            InsightsPartialOrForbidden: false
        );

        var evidence2 = new PageEvaluationEvidence(
            PageId: "page_abc",
            PageName: "ABC Shop",
            FanCount: 200,
            FollowersCount: 200,
            Posts: new[]
            {
                new PagePostEvidence("p1", "Sale sđt 0909999999 inbox shop", now.AddDays(-2), 10, 2, 1, 500, 50, 20, "fresh")
            },
            TotalConversations: 10,
            UnrepliedConversations: 1,
            ConversationsWithPhone: 5,
            TotalLeads: 5,
            HotLeads: 2,
            WarmLeads: 2,
            CommentsPermissionForbidden: false,
            InsightsPartialOrForbidden: false
        );

        var res1 = PageEvaluation.Evaluate(evidence1, now);
        var res2 = PageEvaluation.Evaluate(evidence2, now);

        Assert.Equal(res1.OverallScore, res2.OverallScore);
        Assert.Equal(res1.Label, res2.Label);
        Assert.Equal(res1.Axes.Completeness, res2.Axes.Completeness);
        Assert.Equal(res1.Axes.Content, res2.Axes.Content);
        Assert.Equal(res1.Axes.Inbox, res2.Axes.Inbox);
        Assert.Equal(res1.Axes.Leads, res2.Axes.Leads);
        Assert.Equal(res1.Axes.Engagement, res2.Axes.Engagement);
        Assert.Equal(res1.Reasons.Count, res2.Reasons.Count);
        Assert.Equal("page-eval", res1.ModelId);
        Assert.Equal("1.0", res1.Version);
    }
}
