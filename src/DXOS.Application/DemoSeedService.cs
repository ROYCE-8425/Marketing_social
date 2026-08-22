using DXOS.Domain;

namespace DXOS.Application;

public sealed class DemoSeedService
{
    private readonly CampaignService _campaigns;
    private readonly LeadService _leads;
    private readonly ILeadStore _leadStore;

    public DemoSeedService(CampaignService campaigns, LeadService leads, ILeadStore leadStore)
    {
        _campaigns = campaigns;
        _leads = leads;
        _leadStore = leadStore;
    }

    public async Task<DemoSeedResult> SeedAsync(CancellationToken cancellationToken)
    {
        await _leadStore.RememberSalesActorAsync("kinh-doanh-an", cancellationToken);
        await _leadStore.RememberSalesActorAsync("kinh-doanh-binh", cancellationToken);

        var marketer = new ActorContext(ActorRole.Marketer, "chuyen-vien-mai");
        var campaign = await _campaigns.CreateDraftAsync(marketer, "Chien dich demo khai truong", cancellationToken);
        campaign = await _campaigns.SendToOwnerAsync(marketer, campaign.Id, cancellationToken);

        var lead80 = await _leads.IntakeFormAsync("Nguyen Van A", "0901234567", "a@example.com", campaign.Id, cancellationToken);
        var lead50 = await _leads.IntakeFormAsync("Le Thi B", "0907654321", null, campaign.Id, cancellationToken);

        return new DemoSeedResult(campaign, new[] { lead80, lead50 });
    }
}

public sealed record DemoSeedResult(Campaign Campaign, IReadOnlyList<Lead> Leads);
