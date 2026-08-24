namespace DXOS.Infrastructure.Persistence.Entities;

public sealed class SocialPostRecord
{
    public string Id { get; set; } = string.Empty;
    public string PostId { get; set; } = string.Empty;
    public string PageId { get; set; } = string.Empty;
    public string? Message { get; set; }
    public string? PermalinkUrl { get; set; }
    public string? FullPicture { get; set; }
    public string? MediaType { get; set; } // "photo", "video", "album", "status", "unknown"
    public string? MediaUrl { get; set; }
    public string? ThumbnailUrl { get; set; }
    public string Status { get; set; } = "published"; // "published", "scheduled", "failed", "cancelled"
    public DateTimeOffset? ScheduledAtUtc { get; set; }
    public bool GraphScheduled { get; set; }
    public long? ReactionCount { get; set; } = 0;
    public long? CommentCount { get; set; } = 0;
    public long? ShareCount { get; set; } = 0;
    public DateTimeOffset? CreatedTimeUtc { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class SocialCommentRecord
{
    public string Id { get; set; } = string.Empty;
    public string CommentId { get; set; } = string.Empty;
    public string PostId { get; set; } = string.Empty;
    public string? FromId { get; set; }
    public string? FromName { get; set; }
    public string? Message { get; set; }
    public string? ParentCommentId { get; set; }
    public bool IsHidden { get; set; }
    public DateTimeOffset? CreatedTimeUtc { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class SocialPostMetricRecord
{
    public string Id { get; set; } = string.Empty;
    public string PostId { get; set; } = string.Empty;
    public long Impressions { get; set; }
    public long EngagedUsers { get; set; }
    public long Clicks { get; set; }
    public string Source { get; set; } = "graph";
    public string DataFreshness { get; set; } = "fresh"; // "fresh", "partial", "cached"
    public DateTimeOffset FetchedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}
