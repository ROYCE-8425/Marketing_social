using DXOS.Application;
using DXOS.Infrastructure.Integrations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace DXOS.Infrastructure.Persistence;

public sealed class PageHealthStore : IPageHealthStore
{
    private readonly BootstrapDbContext _db;
    private readonly FacebookPageClient? _fbClient;
    private readonly IConfiguration _config;
    private readonly ILogger<PageHealthStore> _logger;

    public PageHealthStore(
        BootstrapDbContext db,
        IConfiguration config,
        ILogger<PageHealthStore> logger,
        FacebookPageClient? fbClient = null)
    {
        _db = db;
        _config = config;
        _logger = logger;
        _fbClient = fbClient;
    }

    public async Task<PageHealthData> GetHealthDataAsync(string pageId, CancellationToken cancellationToken = default)
    {
        var cleanPageId = string.IsNullOrWhiteSpace(pageId)
            ? _config["FACEBOOK_PAGE_ID"] ?? "988656934325292"
            : pageId;

        // 1. Posts & Metrics
        var posts = await _db.SocialPosts.AsNoTracking()
            .Where(p => p.PageId == cleanPageId || string.IsNullOrEmpty(cleanPageId))
            .OrderByDescending(p => p.CreatedTimeUtc ?? p.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        var metrics = await _db.SocialPostMetrics.AsNoTracking().ToListAsync(cancellationToken);
        var metricDict = metrics.ToDictionary(m => m.PostId, StringComparer.OrdinalIgnoreCase);

        var postDataList = posts.Select(p =>
        {
            metricDict.TryGetValue(p.PostId, out var m);
            return new PagePostData(
                PostId: p.PostId,
                Message: p.Message,
                CreatedTimeUtc: p.CreatedTimeUtc,
                ReactionCount: p.ReactionCount ?? 0,
                CommentCount: p.CommentCount ?? 0,
                ShareCount: p.ShareCount ?? 0,
                Impressions: m?.Impressions ?? 0,
                EngagedUsers: m?.EngagedUsers ?? 0,
                Clicks: m?.Clicks ?? 0,
                DataFreshness: m?.DataFreshness ?? "none");
        }).ToList();

        // 2. Page info (DB + Graph API if configured)
        var dbPage = await _db.SocialPages.AsNoTracking().FirstOrDefaultAsync(p => p.Id == cleanPageId, cancellationToken);
        string? pageName = dbPage?.Name ?? "Royce Shop";
        long? fanCount = null;
        long? followersCount = null;

        var token = _config["FACEBOOK_PAGE_ACCESS_TOKEN"] ?? _config["Facebook:PageAccessToken"];
        if (_fbClient is not null && !string.IsNullOrWhiteSpace(token))
        {
            try
            {
                var fbInfo = await _fbClient.GetPageAsync(cleanPageId, token, cancellationToken);
                if (fbInfo is not null)
                {
                    pageName = fbInfo.Name ?? pageName;
                    fanCount = fbInfo.FanCount;
                    followersCount = fbInfo.FollowersCount;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not fetch live page info for page {PageId}", cleanPageId);
            }
        }

        // 3. Conversations
        var totalConvs = await _db.SocialConversations.AsNoTracking()
            .CountAsync(c => string.IsNullOrEmpty(cleanPageId) || c.PageId == cleanPageId, cancellationToken);

        var unrepliedConvs = await _db.SocialConversations.AsNoTracking()
            .CountAsync(c => (string.IsNullOrEmpty(cleanPageId) || c.PageId == cleanPageId) && !c.IsReplied, cancellationToken);

        var phoneConvs = await _db.SocialConversations.AsNoTracking()
            .CountAsync(c => (string.IsNullOrEmpty(cleanPageId) || c.PageId == cleanPageId) && c.HasPhone, cancellationToken);

        // 4. Leads
        var totalLeads = await _db.Leads.AsNoTracking().CountAsync(cancellationToken);
        var hotLeads = await _db.Leads.AsNoTracking().CountAsync(l => l.Label == "Hot", cancellationToken);
        var warmLeads = await _db.Leads.AsNoTracking().CountAsync(l => l.Label == "Warm", cancellationToken);

        // 5. Permission & Freshness indicators
        bool commentsForbidden = false;
        bool commentsHttpSuccess = false;

        var probePost = postDataList.FirstOrDefault(p =>
            !string.IsNullOrWhiteSpace(p.PostId) &&
            (p.PostId.Contains('_') || p.PostId.StartsWith(cleanPageId, StringComparison.OrdinalIgnoreCase)));

        if (_fbClient is not null && !string.IsNullOrWhiteSpace(token) && probePost is not null)
        {
            try
            {
                var commentsRes = await _fbClient.GetPostCommentsAsync(probePost.PostId, token, cancellationToken);
                commentsForbidden = commentsRes.HasPermissionError;
                commentsHttpSuccess = commentsRes.HttpSuccess;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Could not probe comments permission for page {PageId}", cleanPageId);
                commentsForbidden = false;
                commentsHttpSuccess = false;
            }
        }
        bool insightsPartial = postDataList.Any(p => p.DataFreshness is "partial" or "none" or "unknown");

        string commentsStatus = "unknown";
        if (commentsForbidden)
        {
            commentsStatus = "forbidden";
        }
        else if (commentsHttpSuccess)
        {
            commentsStatus = "ok";
        }
        else
        {
            commentsStatus = "unknown";
        }

        return new PageHealthData(
            PageId: cleanPageId,
            PageName: pageName,
            FanCount: fanCount,
            FollowersCount: followersCount,
            Posts: postDataList,
            TotalConversations: totalConvs,
            UnrepliedConversations: unrepliedConvs,
            ConversationsWithPhone: phoneConvs,
            TotalLeads: totalLeads,
            HotLeads: hotLeads,
            WarmLeads: warmLeads,
            CommentsPermissionForbidden: commentsForbidden,
            InsightsPartialOrForbidden: insightsPartial,
            CommentsStatus: commentsStatus);
    }

    public async Task<IReadOnlyList<UnrepliedConversationData>> GetUnrepliedConversationsAsync(
        string pageId,
        int limit = 10,
        CancellationToken cancellationToken = default)
    {
        var cleanPageId = string.IsNullOrWhiteSpace(pageId)
            ? _config["FACEBOOK_PAGE_ID"] ?? "988656934325292"
            : pageId;

        var convs = await _db.SocialConversations
            .Where(c => (string.IsNullOrEmpty(cleanPageId) || c.PageId == cleanPageId) && !c.IsReplied)
            .OrderByDescending(c => c.UpdatedAt)
            .Take(limit)
            .ToListAsync(cancellationToken);

        bool hasUpdates = false;
        var result = new List<UnrepliedConversationData>(convs.Count);

        foreach (var conv in convs)
        {
            var phone = conv.CustomerPhone;
            if (string.IsNullOrWhiteSpace(phone))
            {
                var extracted = PhoneExtractor.ExtractFirstPhoneNumber(conv.Snippet);
                if (!string.IsNullOrWhiteSpace(extracted))
                {
                    phone = extracted;
                    conv.CustomerPhone = extracted;
                    conv.HasPhone = true;
                    conv.UpdatedAt = DateTimeOffset.UtcNow;
                    hasUpdates = true;
                }
            }

            result.Add(new UnrepliedConversationData(
                conv.Id,
                conv.CustomerName,
                conv.Snippet,
                phone,
                conv.AssignedToActor));
        }

        if (hasUpdates)
        {
            try
            {
                await _db.SaveChangesAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not persist extracted phone numbers for unreplied conversations");
            }
        }

        return result;
    }
}
