using DXOS.Application;
using DXOS.Domain;
using DXOS.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DXOS.Unit.Tests;

public sealed class CampaignStudioTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 25, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Campaign_WithNoProduct_RoundTripsSuccessfully()
    {
        var campaign = Campaign.CreateDraft(
            topic: "Zero Product Campaign",
            copy: "Brand story copy",
            createdByActor: "marketer_bob",
            nowUtc: Now,
            kind: "promotion",
            description: "A brand awareness campaign with no attached SKU",
            platformsJson: "[\"facebook\"]",
            eventStartUtc: null,
            eventEndUtc: null,
            location: null,
            imageUrlsJson: "[\"https://cdn.royceshop.vn/banner.jpg\"]",
            landingUrl: "https://royceshop.vn",
            productName: null,
            productPriceVnd: null,
            productSku: null,
            productImageUrl: null);

        Assert.Equal(CampaignStatus.Draft, campaign.Status);
        Assert.Equal("promotion", campaign.Kind);
        Assert.Null(campaign.ProductName);
        Assert.Null(campaign.ProductPriceVnd);
        Assert.Null(campaign.ProductSku);
        Assert.Null(campaign.ProductImageUrl);
        Assert.Equal("[\"https://cdn.royceshop.vn/banner.jpg\"]", campaign.ImageUrlsJson);
        Assert.Equal("https://royceshop.vn", campaign.LandingUrl);
    }

    [Fact]
    public void Campaign_WithEventKind_StoresEventStartAndEndUtc()
    {
        var startUtc = Now.AddDays(2);
        var endUtc = Now.AddDays(4);

        var campaign = Campaign.CreateDraft(
            topic: "Grand Opening Workshop",
            copy: string.Empty,
            createdByActor: "marketer_bob",
            nowUtc: Now,
            kind: "event",
            description: "Opening showroom workshop",
            platformsJson: "[\"facebook\"]",
            eventStartUtc: startUtc,
            eventEndUtc: endUtc,
            location: "123 Le Loi, Q1, HCMC",
            imageUrlsJson: null,
            landingUrl: "https://royceshop.vn/workshop",
            productName: null,
            productPriceVnd: null,
            productSku: null,
            productImageUrl: null);

        Assert.Equal("event", campaign.Kind);
        Assert.Equal(startUtc, campaign.EventStartUtc);
        Assert.Equal(endUtc, campaign.EventEndUtc);
        Assert.Equal("123 Le Loi, Q1, HCMC", campaign.Location);
    }

    [Fact]
    public void Campaign_UpdateBrief_AllowedInDraft_RejectedWhenPublished()
    {
        var campaign = Campaign.CreateDraft(
            topic: "Draft Campaign",
            copy: "Old copy",
            createdByActor: "marketer_bob",
            nowUtc: Now,
            kind: "promotion",
            description: "Initial description",
            platformsJson: null,
            eventStartUtc: null,
            eventEndUtc: null,
            location: null,
            imageUrlsJson: null,
            landingUrl: null,
            productName: null,
            productPriceVnd: null,
            productSku: null,
            productImageUrl: null);

        // Allowed in Draft
        campaign.UpdateBrief(
            topic: "Updated Title",
            copy: "New copy",
            kind: "product_launch",
            description: "Updated description",
            platformsJson: "[\"facebook\"]",
            eventStartUtc: null,
            eventEndUtc: null,
            location: null,
            imageUrlsJson: null,
            landingUrl: "https://royceshop.vn/new",
            productName: "New Product",
            productPriceVnd: 500000m,
            productSku: "SKU-01",
            productImageUrl: "https://cdn.royceshop.vn/prod.jpg",
            nowUtc: Now.AddMinutes(5));

        Assert.Equal("Updated Title", campaign.Topic);
        Assert.Equal("product_launch", campaign.Kind);
        Assert.Equal("New Product", campaign.ProductName);
        Assert.Equal(500000m, campaign.ProductPriceVnd);

        // Transition through approval
        campaign.SubmitReview(ActorRole.Marketer, Now.AddMinutes(10));
        campaign.SubmitReview(ActorRole.Marketer, Now.AddMinutes(15));
        campaign.Approve(ActorRole.Owner, Now.AddMinutes(20));
        Assert.Equal(CampaignStatus.Published, campaign.Status);

        // Forbidden when Published
        var ex = Assert.Throws<DomainRuleException>(() => campaign.UpdateBrief(
            topic: "Illegal Update",
            copy: "Forbidden",
            kind: "other",
            description: null,
            platformsJson: null,
            eventStartUtc: null,
            eventEndUtc: null,
            location: null,
            imageUrlsJson: null,
            landingUrl: null,
            productName: null,
            productPriceVnd: null,
            productSku: null,
            productImageUrl: null,
            nowUtc: Now.AddMinutes(25)));

        Assert.Equal("InvalidTransition", ex.Code);
    }

    [Fact]
    public async Task CampaignService_AiDrafts_ReturnsThreeCaptions_AndDoesNotAutoPublish()
    {
        var store = new InMemoryCampaignStore();
        var service = new CampaignService(store, new CampaignCopyStub(), new TestClock(Now));
        var mockChatClient = new MockChatClient(NullLogger<MockChatClient>.Instance);

        var actor = new ActorContext(ActorRole.Marketer, "marketer_bob");
        var draftDto = new CreateCampaignDraftDto(
            Title: "Summer Flash Sale",
            Topic: "Summer Flash Sale",
            Kind: "promotion",
            Description: "Ưu đãi 30% bộ sưu tập hè",
            Platforms: ["facebook"],
            EventStart: null,
            EventEnd: null,
            Location: "Royce Shop",
            ImageUrls: ["https://royceshop.vn/summer.jpg"],
            LandingUrl: "https://royceshop.vn/summer",
            Product: new CampaignProductDto("Áo Polo", 299000m, "POLO-01", "https://royceshop.vn/polo.jpg"));

        var created = await service.CreateDraftAsync(actor, draftDto, CancellationToken.None);
        Assert.Equal("Summer Flash Sale", created.Topic);
        Assert.Equal(CampaignStatus.Draft, created.Status);

        // Generate AI drafts
        var aiResult = await service.GenerateAiDraftsAsync(actor, created.Id, mockChatClient, CancellationToken.None);
        Assert.NotNull(aiResult);
        Assert.Equal(3, aiResult.Drafts.Count);
        Assert.Contains("AI không tự", aiResult.Disclaimer, StringComparison.OrdinalIgnoreCase);

        // Verify status remains Draft (AI does NOT auto-publish or approve)
        var fresh = await service.GetAsync(created.Id, CancellationToken.None);
        Assert.NotNull(fresh);
        Assert.Equal(CampaignStatus.Draft, fresh.Status);

        // Apply draft copy
        var chosenCaption = aiResult.Drafts[0].Caption;
        var updated = await service.ApplyDraftCopyAsync(actor, created.Id, chosenCaption, CancellationToken.None);
        Assert.Equal(chosenCaption, updated.Copy);
        Assert.Equal(CampaignStatus.Draft, updated.Status);
    }

    private sealed class InMemoryCampaignStore : ICampaignStore
    {
        private readonly Dictionary<Guid, Campaign> _items = new();

        public Task AddAsync(Campaign campaign, CancellationToken cancellationToken = default)
        {
            _items[campaign.Id] = campaign;
            return Task.CompletedTask;
        }

        public Task<Campaign?> GetAsync(Guid id, CancellationToken cancellationToken = default)
        {
            _items.TryGetValue(id, out var camp);
            return Task.FromResult(camp);
        }

        public Task<IReadOnlyList<Campaign>> ListAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<Campaign>>(_items.Values.ToList());
        }

        public Task UpdateAsync(Campaign campaign, CancellationToken cancellationToken = default)
        {
            _items[campaign.Id] = campaign;
            return Task.CompletedTask;
        }
    }

    private sealed class TestClock : IClock
    {
        public DateTimeOffset UtcNow { get; }

        public TestClock(DateTimeOffset utcNow)
        {
            UtcNow = utcNow;
        }
    }
}
