using System.Text.Json;
using DXOS.Application.Abstractions;
using DXOS.Domain;

namespace DXOS.Application;

public sealed record CampaignProductDto(
    string? Name,
    decimal? PriceVnd,
    string? Sku,
    string? ImageUrl);

public sealed record CreateCampaignDraftDto(
    string? Title,
    string? Topic,
    string? Kind,
    string? Description,
    IReadOnlyList<string>? Platforms,
    DateTimeOffset? EventStart,
    DateTimeOffset? EventEnd,
    string? Location,
    IReadOnlyList<string>? ImageUrls,
    string? LandingUrl,
    CampaignProductDto? Product);

public sealed record UpdateCampaignBriefDto(
    string? Title,
    string? Topic,
    string? Copy,
    string? Kind,
    string? Description,
    IReadOnlyList<string>? Platforms,
    DateTimeOffset? EventStart,
    DateTimeOffset? EventEnd,
    string? Location,
    IReadOnlyList<string>? ImageUrls,
    string? LandingUrl,
    CampaignProductDto? Product);

public sealed record CampaignAiDraftItem(
    string Caption,
    string? SuggestedMediaUrl,
    string? ScheduleHintLocal);

public sealed record CampaignAiDraftsResult(
    IReadOnlyList<CampaignAiDraftItem> Drafts,
    string Disclaimer);

public sealed class CampaignService
{
    private readonly ICampaignStore _store;
    private readonly CampaignCopyStub _copyStub;
    private readonly IClock _clock;

    public CampaignService(ICampaignStore store, CampaignCopyStub copyStub, IClock clock)
    {
        _store = store;
        _copyStub = copyStub;
        _clock = clock;
    }

    public async Task<Campaign> CreateDraftAsync(ActorContext actor, string topic, CancellationToken cancellationToken)
    {
        return await CreateDraftAsync(actor, new CreateCampaignDraftDto(topic, topic, "other", null, null, null, null, null, null, null, null), cancellationToken);
    }

    public async Task<Campaign> CreateDraftAsync(ActorContext actor, CreateCampaignDraftDto dto, CancellationToken cancellationToken)
    {
        EnsureActor(actor);
        if (actor.Role == ActorRole.Sales)
        {
            throw new DomainRuleException("ForbiddenRole", "Sales cannot create campaigns.");
        }

        var topic = !string.IsNullOrWhiteSpace(dto.Title) ? dto.Title : (dto.Topic ?? string.Empty);
        if (string.IsNullOrWhiteSpace(topic))
        {
            throw new DomainRuleException("InvalidTopic", "Campaign topic is required.");
        }

        var copy = _copyStub.DraftFromTopic(topic);
        var platformsJson = dto.Platforms is { Count: > 0 }
            ? JsonSerializer.Serialize(dto.Platforms)
            : "[\"facebook\"]";
        var imageUrlsJson = dto.ImageUrls is { Count: > 0 }
            ? JsonSerializer.Serialize(dto.ImageUrls)
            : "[]";

        var campaign = Campaign.CreateDraft(
            topic,
            copy,
            actor.ActorId,
            _clock.UtcNow,
            kind: dto.Kind,
            description: dto.Description,
            platformsJson: platformsJson,
            eventStartUtc: dto.EventStart,
            eventEndUtc: dto.EventEnd,
            location: dto.Location,
            imageUrlsJson: imageUrlsJson,
            landingUrl: dto.LandingUrl,
            productName: dto.Product?.Name,
            productPriceVnd: dto.Product?.PriceVnd,
            productSku: dto.Product?.Sku,
            productImageUrl: dto.Product?.ImageUrl);

        await _store.AddAsync(campaign, cancellationToken);
        return campaign;
    }

    public async Task<Campaign> UpdateBriefAsync(ActorContext actor, Guid campaignId, UpdateCampaignBriefDto dto, CancellationToken cancellationToken)
    {
        EnsureActor(actor);
        var campaign = await GetRequiredAsync(campaignId, cancellationToken);

        var topic = !string.IsNullOrWhiteSpace(dto.Title) ? dto.Title : (!string.IsNullOrWhiteSpace(dto.Topic) ? dto.Topic : campaign.Topic);
        var copy = dto.Copy ?? campaign.Copy;
        var platformsJson = dto.Platforms is { Count: > 0 }
            ? JsonSerializer.Serialize(dto.Platforms)
            : campaign.PlatformsJson;
        var imageUrlsJson = dto.ImageUrls is { Count: > 0 }
            ? JsonSerializer.Serialize(dto.ImageUrls)
            : campaign.ImageUrlsJson;

        campaign.UpdateBrief(
            topic,
            copy,
            dto.Kind ?? campaign.Kind,
            dto.Description ?? campaign.Description,
            platformsJson,
            dto.EventStart ?? campaign.EventStartUtc,
            dto.EventEnd ?? campaign.EventEndUtc,
            dto.Location ?? campaign.Location,
            imageUrlsJson,
            dto.LandingUrl ?? campaign.LandingUrl,
            dto.Product != null ? dto.Product.Name : campaign.ProductName,
            dto.Product != null ? dto.Product.PriceVnd : campaign.ProductPriceVnd,
            dto.Product != null ? dto.Product.Sku : campaign.ProductSku,
            dto.Product != null ? dto.Product.ImageUrl : campaign.ProductImageUrl,
            _clock.UtcNow);

        await _store.UpdateAsync(campaign, cancellationToken);
        return campaign;
    }

    public async Task<Campaign> ApplyDraftCopyAsync(ActorContext actor, Guid campaignId, string caption, CancellationToken cancellationToken)
    {
        EnsureActor(actor);
        var campaign = await GetRequiredAsync(campaignId, cancellationToken);
        if (string.IsNullOrWhiteSpace(caption))
        {
            throw new DomainRuleException("InvalidCopy", "Caption cannot be empty.");
        }
        campaign.UpdateCopy(caption.Trim(), _clock.UtcNow);
        await _store.UpdateAsync(campaign, cancellationToken);
        return campaign;
    }

    public async Task<CampaignAiDraftsResult> GenerateAiDraftsAsync(
        ActorContext actor,
        Guid campaignId,
        IChatClient chatClient,
        CancellationToken cancellationToken)
    {
        EnsureActor(actor);
        var campaign = await GetRequiredAsync(campaignId, cancellationToken);

        var systemPrompt = """
            Bạn là DX-OS Campaign AI Content Strategist.
            Nhiệm vụ của bạn là dựa trên thông tin brief chiến dịch marketing được cung cấp để gợi ý 3 bản thảo bài đăng Facebook (captions) sáng tạo, chuẩn SEO và thu hút tương tác.

            Quy tắc bắt buộc:
            1. Trả về đúng JSON theo định dạng:
            {
              "drafts": [
                {
                  "caption": "Nội dung bài viết Facebook đầy đủ bao gồm headline, thân bài, bullet points và Call to Action",
                  "suggestedMediaUrl": "URL hình ảnh được đề xuất từ brief (nếu có) hoặc để null",
                  "scheduleHintLocal": "Gợi ý khung giờ đăng bài tối ưu tại Việt Nam (ví dụ: '11:30 hôm nay' hoặc '20:00 ngày mai')"
                }
              ],
              "disclaimer": "AI không tự đăng bài, không tự gửi tin, không chi tiền."
            }
            2. Số lượng bản thảo bài viết: đúng 3 bản (mỗi bản có phong cách khác nhau: khuyến mãi trực diện, kể chuyện/giá trị, bắt trend/tương tác).
            3. AI tuyệt đối KHÔNG tự động đăng bài, KHÔNG gọi API ngoài, chỉ đề xuất nội dung cho con người duyệt.
            """;

        var briefInfo = new
        {
            topic = campaign.Topic,
            kind = campaign.Kind,
            description = campaign.Description,
            platforms = campaign.PlatformsJson,
            eventStartUtc = campaign.EventStartUtc,
            eventEndUtc = campaign.EventEndUtc,
            location = campaign.Location,
            imageUrls = campaign.ImageUrlsJson,
            landingUrl = campaign.LandingUrl,
            product = string.IsNullOrWhiteSpace(campaign.ProductName) ? null : new
            {
                name = campaign.ProductName,
                priceVnd = campaign.ProductPriceVnd,
                sku = campaign.ProductSku,
                imageUrl = campaign.ProductImageUrl
            }
        };

        var userPrompt = $"Dưới đây là thông tin chiến dịch marketing [chiến dịch ai-draft]:\n{JsonSerializer.Serialize(briefInfo, new JsonSerializerOptions { WriteIndented = true })}\nHãy tạo 3 bản thảo bài viết Facebook chất lượng cao.";

        var rawResponse = await chatClient.CompleteAsync(systemPrompt, userPrompt, cancellationToken);
        return ParseAiDraftsResponse(rawResponse, campaign);
    }

    public async Task<Campaign> SubmitReviewAsync(ActorContext actor, Guid campaignId, CancellationToken cancellationToken)
    {
        EnsureActor(actor);
        var campaign = await GetRequiredAsync(campaignId, cancellationToken);
        campaign.SubmitReview(actor.Role, _clock.UtcNow);
        await _store.UpdateAsync(campaign, cancellationToken);
        return campaign;
    }

    public async Task<Campaign> SendToOwnerAsync(ActorContext actor, Guid campaignId, CancellationToken cancellationToken)
    {
        EnsureActor(actor);
        var campaign = await GetRequiredAsync(campaignId, cancellationToken);
        campaign.SendToOwner(actor.Role, _clock.UtcNow);
        await _store.UpdateAsync(campaign, cancellationToken);
        return campaign;
    }

    public async Task<Campaign> ApproveAsync(ActorContext actor, Guid campaignId, CancellationToken cancellationToken)
    {
        EnsureActor(actor);
        var campaign = await GetRequiredAsync(campaignId, cancellationToken);
        campaign.Approve(actor.Role, _clock.UtcNow);
        await _store.UpdateAsync(campaign, cancellationToken);
        return campaign;
    }

    public async Task<Campaign> UndoApprovalAsync(ActorContext actor, Guid campaignId, CancellationToken cancellationToken)
    {
        EnsureActor(actor);
        var campaign = await GetRequiredAsync(campaignId, cancellationToken);
        campaign.UndoApproval(actor.Role, _clock.UtcNow);
        await _store.UpdateAsync(campaign, cancellationToken);
        return campaign;
    }

    public async Task<Campaign> RejectAsync(ActorContext actor, Guid campaignId, string? reason, CancellationToken cancellationToken)
    {
        EnsureActor(actor);
        var campaign = await GetRequiredAsync(campaignId, cancellationToken);
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new DomainRuleException("InvalidReason", "Lý do từ chối chiến dịch là bắt buộc.");
        }

        campaign.Reject(actor.Role, reason, _clock.UtcNow);
        await _store.UpdateAsync(campaign, cancellationToken);
        return campaign;
    }

    public Task<Campaign?> GetAsync(Guid campaignId, CancellationToken cancellationToken)
    {
        return _store.GetAsync(campaignId, cancellationToken);
    }

    public Task<IReadOnlyList<Campaign>> ListAsync(CancellationToken cancellationToken)
    {
        return _store.ListAsync(cancellationToken);
    }

    private async Task<Campaign> GetRequiredAsync(Guid campaignId, CancellationToken cancellationToken)
    {
        var campaign = await _store.GetAsync(campaignId, cancellationToken);
        if (campaign is null)
        {
            throw new DomainRuleException("NotFound", $"Campaign '{campaignId}' was not found.");
        }

        return campaign;
    }

    private static void EnsureActor(ActorContext actor)
    {
        if (string.IsNullOrWhiteSpace(actor.ActorId))
        {
            throw new DomainRuleException("InvalidActor", "X-DXOS-Actor is required.");
        }
    }

    private static CampaignAiDraftsResult ParseAiDraftsResponse(string rawResponse, Campaign campaign)
    {
        const string disclaimer = "AI không tự đăng bài, không tự gửi tin, không chi tiền.";
        var clean = StripMarkdownFence(rawResponse?.Trim() ?? string.Empty);

        try
        {
            using var doc = JsonDocument.Parse(clean);
            var root = doc.RootElement;
            if (root.TryGetProperty("drafts", out var draftsEl) && draftsEl.ValueKind == JsonValueKind.Array)
            {
                var list = new List<CampaignAiDraftItem>();
                foreach (var item in draftsEl.EnumerateArray())
                {
                    var cap = item.TryGetProperty("caption", out var cProp) ? cProp.GetString() : null;
                    if (string.IsNullOrWhiteSpace(cap))
                    {
                        cap = item.TryGetProperty("content", out var conProp) ? conProp.GetString() : null;
                    }
                    if (string.IsNullOrWhiteSpace(cap)) continue;

                    var media = item.TryGetProperty("suggestedMediaUrl", out var mProp) ? mProp.GetString() : null;
                    var sched = item.TryGetProperty("scheduleHintLocal", out var sProp) ? sProp.GetString() : null;

                    list.Add(new CampaignAiDraftItem(cap, media, sched ?? "11:30 ngày mai"));
                }

                if (list.Count > 0)
                {
                    return new CampaignAiDraftsResult(list, disclaimer);
                }
            }
        }
        catch
        {
            // Fallback parsing below
        }

        var defaultMedia = !string.IsNullOrWhiteSpace(campaign.ProductImageUrl) ? campaign.ProductImageUrl : "/logos/royce_avatar.jpg";
        var fallbackList = new List<CampaignAiDraftItem>
        {
            new($"🔥 [{campaign.Topic.ToUpperInvariant()}]\n{campaign.Description ?? campaign.Copy}\n👉 Inbox ngay để nhận tư vấn và ưu đãi đặc quyền!", defaultMedia, "20:00 hôm nay"),
            new($"⚡ [CƠ HỘI ĐẶC BIỆT]\n{campaign.Topic} - Giải pháp tối ưu dành cho bạn.\nLiên hệ ngay hôm nay để nhận thông tin chi tiết!", defaultMedia, "11:30 ngày mai"),
            new($"🌟 [ƯU ĐÃI NỔI BẬT]\nKhám phá {campaign.Topic} cùng hàng loạt quà tặng hấp dẫn.\nĐăng ký ngay tại: {campaign.LandingUrl ?? "Fanpage"}", defaultMedia, "09:00 cuối tuần")
        };
        return new CampaignAiDraftsResult(fallbackList, disclaimer);
    }

    private static string StripMarkdownFence(string content)
    {
        if (string.IsNullOrWhiteSpace(content)) return string.Empty;
        var text = content.Trim();
        if (text.StartsWith("```json", StringComparison.OrdinalIgnoreCase))
        {
            text = text[7..];
        }
        else if (text.StartsWith("```"))
        {
            text = text[3..];
        }
        if (text.EndsWith("```"))
        {
            text = text[..^3];
        }
        return text.Trim();
    }
}
