namespace DXOS.Infrastructure.Persistence.Entities;

public sealed class CampaignRecord
{
    public Guid Id { get; set; }
    public string Topic { get; set; } = string.Empty;
    public string Copy { get; set; } = string.Empty;
    public string? CopySnapshot { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? RejectionReason { get; set; }
    public DateTimeOffset? ApprovedAtUtc { get; set; }
    public string CreatedByActor { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }

    // Studio brief and platform properties
    public string Kind { get; set; } = "other";
    public string? Description { get; set; }
    public string PlatformsJson { get; set; } = "[\"facebook\"]";
    public DateTimeOffset? EventStartUtc { get; set; }
    public DateTimeOffset? EventEndUtc { get; set; }
    public string? Location { get; set; }
    public string ImageUrlsJson { get; set; } = "[]";
    public string? LandingUrl { get; set; }

    // Optional product properties
    public string? ProductName { get; set; }
    public decimal? ProductPriceVnd { get; set; }
    public string? ProductSku { get; set; }
    public string? ProductImageUrl { get; set; }
}
