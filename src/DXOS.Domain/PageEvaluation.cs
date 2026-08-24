namespace DXOS.Domain;

public enum PageHealthLabel
{
    Healthy,
    Watch,
    Critical
}

public sealed record PagePostEvidence(
    string PostId,
    string? Message,
    DateTimeOffset? CreatedTimeUtc,
    long ReactionCount,
    long CommentCount,
    long ShareCount,
    long Impressions,
    long EngagedUsers,
    long Clicks,
    string DataFreshness); // "fresh", "partial", "unknown", "forbidden", "none"

public sealed record PageEvaluationEvidence(
    string PageId,
    string? PageName,
    long? FanCount,
    long? FollowersCount,
    IReadOnlyList<PagePostEvidence> Posts,
    int TotalConversations,
    int UnrepliedConversations,
    int ConversationsWithPhone,
    int TotalLeads,
    int HotLeads,
    int WarmLeads,
    bool CommentsPermissionForbidden,
    bool InsightsPartialOrForbidden);

public sealed record PageAxisScores(
    int? Content,
    int? Inbox,
    int? Leads,
    int? Engagement,
    double Completeness);

public sealed record PageEvaluationResult(
    int OverallScore,
    PageHealthLabel Label,
    PageAxisScores Axes,
    IReadOnlyList<string> Reasons,
    string ModelId,
    string Version);

public static class PageEvaluation
{
    public const string ModelId = "page-eval";
    public const string Version = "1.0";

    private static readonly string[] CtaKeywords =
    [
        "sđt", "sdt", "số điện thoại", "so dien thoai", "hotline", "inbox", "nhắn tin",
        "nhan tin", "liên hệ", "lien he", "đặt hàng", "dat hang", "mua", "http://", "https://",
        "link", "zalo", "tư vấn", "tu van", "đăng ký", "dang ky", "báo giá", "bao gia"
    ];

    public static PageEvaluationResult Evaluate(PageEvaluationEvidence evidence, DateTimeOffset nowUtc)
    {
        var reasons = new List<string>();

        // 1. Content Axis (0-100)
        int? contentScore = null;
        if (evidence.Posts is not null && evidence.Posts.Count > 0)
        {
            var postsIn14d = evidence.Posts.Count(p => p.CreatedTimeUtc.HasValue && (nowUtc - p.CreatedTimeUtc.Value).TotalDays <= 14);
            int freqScore = postsIn14d switch
            {
                >= 3 => 40,
                >= 1 => 25,
                _ => 10
            };

            var validMsgCount = evidence.Posts.Count(p => !string.IsNullOrWhiteSpace(p.Message) && p.Message.Trim().Length > 20);
            var qualityScore = validMsgCount > 0 ? 30 : 10;

            var hasCta = evidence.Posts.Any(p =>
                !string.IsNullOrWhiteSpace(p.Message) &&
                CtaKeywords.Any(k => p.Message.Contains(k, StringComparison.OrdinalIgnoreCase)));
            var ctaScore = hasCta ? 30 : 10;

            contentScore = Math.Clamp(freqScore + qualityScore + ctaScore, 0, 100);

            if (postsIn14d > 0)
            {
                reasons.Add($"Nội dung: Có {postsIn14d} bài đăng trong 14 ngày qua (Tần suất: {freqScore}/40đ).");
            }
            else
            {
                reasons.Add("Nội dung: Không có bài đăng mới trong 14 ngày qua (Cần duy trì tần suất).");
            }

            if (hasCta)
            {
                reasons.Add("Nội dung: Bài viết có lời kêu gọi hành động (CTA) rõ ràng (+30đ).");
            }
            else
            {
                reasons.Add("Nội dung: Chưa có CTA hoặc thông tin liên hệ trong bài viết.");
            }
        }
        else
        {
            contentScore = 0;
            reasons.Add("Nội dung: Chưa có bài đăng nào được ghi nhận từ Fanpage.");
        }

        // 2. Inbox Axis (0-100 or null)
        int? inboxScore = null;
        if (evidence.TotalConversations > 0)
        {
            var unrepliedRatio = (double)evidence.UnrepliedConversations / evidence.TotalConversations;
            int replyScore = unrepliedRatio switch
            {
                <= 0.10 => 50,
                <= 0.30 => 35,
                <= 0.50 => 20,
                _ => 5
            };

            var phoneRatio = (double)evidence.ConversationsWithPhone / evidence.TotalConversations;
            int phoneScore = phoneRatio switch
            {
                >= 0.50 => 50,
                >= 0.25 => 35,
                >= 0.10 => 20,
                _ => 10
            };

            inboxScore = Math.Clamp(replyScore + phoneScore, 0, 100);
            reasons.Add($"Hộp thư: {evidence.TotalConversations} hội thoại. Tỉ lệ chưa trả lời {unrepliedRatio:P0}, tỉ lệ có SĐT {phoneRatio:P0} ({inboxScore}/100đ).");
        }
        else
        {
            inboxScore = null;
            reasons.Add("Hộp thư: Chưa có dữ liệu hội thoại trong hệ thống CRM.");
        }

        // 3. Leads Axis (0-100 or null)
        int? leadsScore = null;
        if (evidence.TotalLeads > 0)
        {
            int volScore = evidence.TotalLeads switch
            {
                >= 10 => 50,
                >= 5 => 35,
                >= 1 => 20,
                _ => 10
            };

            var hotWarmRatio = (double)(evidence.HotLeads + evidence.WarmLeads) / evidence.TotalLeads;
            int qualScore = hotWarmRatio switch
            {
                >= 0.50 => 50,
                >= 0.25 => 35,
                > 0 => 20,
                _ => 10
            };

            leadsScore = Math.Clamp(volScore + qualScore, 0, 100);
            reasons.Add($"Leads: {evidence.TotalLeads} khách tiềm năng ({evidence.HotLeads} HOT, {evidence.WarmLeads} WARM) ({leadsScore}/100đ).");
        }
        else if (evidence.TotalConversations > 0)
        {
            leadsScore = 20;
            reasons.Add("Leads: Chưa có lead nào được chuyển đổi từ hội thoại hộp thư.");
        }
        else
        {
            leadsScore = null;
            reasons.Add("Leads: Chưa có dữ liệu khách hàng tiềm năng.");
        }

        // 4. Engagement Axis (0-100 or null)
        int? engagementScore = null;
        var hasRealEngagementData = evidence.Posts is not null && evidence.Posts.Any(p =>
            (p.DataFreshness == "fresh" || p.DataFreshness == "partial") &&
            (p.ReactionCount > 0 || p.CommentCount > 0 || p.ShareCount > 0 || p.Clicks > 0 || p.Impressions > 0));

        if (evidence.CommentsPermissionForbidden || (!hasRealEngagementData && (evidence.InsightsPartialOrForbidden || evidence.Posts?.All(p => p.DataFreshness is "none" or "unknown" or "forbidden") == true)))
        {
            engagementScore = null;
            reasons.Add("Tương tác: Thiếu quyền hoặc metric Graph không hỗ trợ — không kết luận flop.");
        }
        else if (hasRealEngagementData)
        {
            long totalInteractions = evidence.Posts!.Sum(p => p.ReactionCount + p.CommentCount + p.ShareCount + p.Clicks);
            var avgInteractions = (double)totalInteractions / Math.Max(1, evidence.Posts!.Count);

            engagementScore = avgInteractions switch
            {
                >= 50 => 100,
                >= 20 => 80,
                >= 5 => 60,
                >= 1 => 40,
                _ => 20
            };
            reasons.Add($"Tương tác: Trung bình {avgInteractions:F1} tương tác/bài viết ({engagementScore}/100đ).");
        }
        else
        {
            engagementScore = null;
            reasons.Add("Tương tác: Chưa có dữ liệu tương tác khả dụng.");
        }

        // 5. Completeness & Overall Score
        int measurableAxes = 0;
        int sumScore = 0;

        if (contentScore.HasValue) { measurableAxes++; sumScore += contentScore.Value; }
        if (inboxScore.HasValue) { measurableAxes++; sumScore += inboxScore.Value; }
        if (leadsScore.HasValue) { measurableAxes++; sumScore += leadsScore.Value; }
        if (engagementScore.HasValue) { measurableAxes++; sumScore += engagementScore.Value; }

        var completeness = measurableAxes / 4.0;
        var overallScore = measurableAxes > 0 ? (int)Math.Round((double)sumScore / measurableAxes) : 0;

        // Label rules: If completeness < 0.5, cannot be Healthy even if content is 100!
        PageHealthLabel label;
        if (completeness < 0.5)
        {
            label = overallScore >= 45 ? PageHealthLabel.Watch : PageHealthLabel.Critical;
            reasons.Add($"Độ đầy đủ: {completeness:P0} (< 50%) — Khuyến nghị cấu hình thêm quyền Graph API để đánh giá chính xác.");
        }
        else
        {
            label = overallScore switch
            {
                >= 75 => PageHealthLabel.Healthy,
                >= 45 => PageHealthLabel.Watch,
                _ => PageHealthLabel.Critical
            };
        }

        var axes = new PageAxisScores(
            Content: contentScore,
            Inbox: inboxScore,
            Leads: leadsScore,
            Engagement: engagementScore,
            Completeness: completeness);

        return new PageEvaluationResult(
            OverallScore: overallScore,
            Label: label,
            Axes: axes,
            Reasons: reasons,
            ModelId: ModelId,
            Version: Version);
    }
}
