using DXOS.Domain;

namespace DXOS.Application;

public sealed class LeadService
{
    private readonly ILeadStore _store;
    private readonly IClock _clock;

    public LeadService(ILeadStore store, IClock clock)
    {
        _store = store;
        _clock = clock;
    }

    public async Task<Lead> IntakeFormAsync(
        string name,
        string? phone,
        string? email,
        Guid? campaignId,
        CancellationToken cancellationToken)
    {
        var now = _clock.UtcNow;
        var normalizedPhone = PhoneNormalizer.Normalize(phone);
        var normalizedEmail = EmailValidator.Normalize(email);

        var existing = await _store.FindByPhoneOrEmailAsync(normalizedPhone, normalizedEmail, cancellationToken);
        if (existing is not null)
        {
            existing.AddInteraction(LeadSource.Form, campaignId, name, now);
            await _store.UpdateAsync(existing, cancellationToken);
            return existing;
        }

        var (_, label, _, _) = LeadScoring.Calculate(name, normalizedPhone, normalizedEmail, LeadSource.Form, campaignId, now);
        string? assigned = null;
        if (label is LeadLabel.Hot or LeadLabel.Warm)
        {
            var salesActors = await _store.ListSalesActorsAsync(cancellationToken);
            var lastAssigned = await _store.GetLastAssignedSalesActorAsync(cancellationToken);
            assigned = SalesRoundRobin.Next(salesActors, lastAssigned);
        }

        var lead = Lead.Intake(name, phone, email, LeadSource.Form, campaignId, assigned, now);
        await _store.AddAsync(lead, cancellationToken);
        if (!string.IsNullOrWhiteSpace(assigned))
        {
            await _store.SetLastAssignedSalesActorAsync(assigned, cancellationToken);
        }

        return lead;
    }

    public async Task<Lead> RecordMessageOrCallAsync(
        string name,
        string? phone,
        string? email,
        LeadSource source,
        Guid? campaignId,
        CancellationToken cancellationToken)
    {
        if (source is not (LeadSource.Message or LeadSource.Call))
        {
            throw new DomainRuleException("InvalidSource", "Only Message or Call records can be stored without inbox integration.");
        }

        var now = _clock.UtcNow;
        var normalizedPhone = PhoneNormalizer.Normalize(phone);
        var normalizedEmail = EmailValidator.Normalize(email);

        var existing = await _store.FindByPhoneOrEmailAsync(normalizedPhone, normalizedEmail, cancellationToken);
        if (existing is not null)
        {
            existing.AddInteraction(source, campaignId, name, now);
            await _store.UpdateAsync(existing, cancellationToken);
            return existing;
        }

        var (_, label, _, _) = LeadScoring.Calculate(name, normalizedPhone, normalizedEmail, source, campaignId, now);
        string? assigned = null;
        if (label is LeadLabel.Hot or LeadLabel.Warm)
        {
            var salesActors = await _store.ListSalesActorsAsync(cancellationToken);
            var lastAssigned = await _store.GetLastAssignedSalesActorAsync(cancellationToken);
            assigned = SalesRoundRobin.Next(salesActors, lastAssigned);
        }

        var lead = Lead.Intake(name, phone, email, source, campaignId, assigned, now);
        await _store.AddAsync(lead, cancellationToken);
        if (!string.IsNullOrWhiteSpace(assigned))
        {
            await _store.SetLastAssignedSalesActorAsync(assigned, cancellationToken);
        }

        return lead;
    }

    public async Task<IReadOnlyList<Lead>> ListAsync(CancellationToken cancellationToken)
    {
        var leads = await _store.ListAsync(cancellationToken);
        var now = _clock.UtcNow;
        foreach (var lead in leads)
        {
            if (lead.ReleaseIfExpired(now))
            {
                await _store.UpdateAsync(lead, cancellationToken);
            }
        }

        return leads;
    }

    public async Task<Lead> ClaimAsync(ActorContext actor, Guid leadId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(actor.ActorId))
        {
            throw new DomainRuleException("InvalidActor", "X-DXOS-Actor is required.");
        }

        var lead = await _store.GetAsync(leadId, cancellationToken);
        if (lead is null)
        {
            throw new DomainRuleException("NotFound", $"Lead '{leadId}' was not found.");
        }

        lead.Claim(actor.Role, actor.ActorId, _clock.UtcNow);
        await _store.UpdateAsync(lead, cancellationToken);
        await _store.RememberSalesActorAsync(actor.ActorId, cancellationToken);
        return lead;
    }

    public async Task<Lead> RejectAsync(ActorContext actor, Guid leadId, string reason, CancellationToken cancellationToken)
    {
        if (actor.Role != ActorRole.Sales)
        {
            throw new DomainRuleException("ForbiddenRole", "Chỉ Sales mới có quyền từ chối lead.");
        }

        if (string.IsNullOrWhiteSpace(actor.ActorId))
        {
            throw new DomainRuleException("InvalidActor", "X-DXOS-Actor is required.");
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new DomainRuleException("InvalidReason", "Lý do từ chối lead là bắt buộc.");
        }

        var lead = await _store.GetAsync(leadId, cancellationToken);
        if (lead is null)
        {
            throw new DomainRuleException("NotFound", $"Lead '{leadId}' was not found.");
        }

        var now = _clock.UtcNow;
        var allSales = await _store.ListSalesActorsAsync(cancellationToken);
        var excluded = lead.RejectedByActors.Concat([actor.ActorId]).ToHashSet(StringComparer.Ordinal);
        var availableSales = allSales.Where(s => !excluded.Contains(s)).ToList();

        var lastAssigned = await _store.GetLastAssignedSalesActorAsync(cancellationToken);
        var nextSales = SalesRoundRobin.Next(availableSales, lastAssigned);

        lead.Reject(actor.Role, actor.ActorId, reason, nextSales, now);
        await _store.UpdateAsync(lead, cancellationToken);
        if (!string.IsNullOrWhiteSpace(nextSales))
        {
            await _store.SetLastAssignedSalesActorAsync(nextSales, cancellationToken);
        }

        return lead;
    }

    public async Task<CplDashboard> GetCplAsync(
        decimal? spendOverride,
        decimal? dailySpend,
        decimal? budget,
        decimal storedSpend,
        CancellationToken cancellationToken)
    {
        var leads = await ListAsync(cancellationToken);
        var leadCount = leads.Count;
        var effectiveSpend = spendOverride.HasValue
            ? (spendOverride.Value < 0 ? 0 : spendOverride.Value)
            : (storedSpend < 0 ? 0 : storedSpend);
        var cpl = leadCount == 0 ? 0 : decimal.Round(effectiveSpend / leadCount, 0, MidpointRounding.AwayFromZero);
        var safeDaily = dailySpend is null or < 0 ? 0 : dailySpend.Value;
        var safeBudget = budget is null or < 0 ? 0 : budget.Value;
        var remaining = safeBudget > 0 ? safeBudget - effectiveSpend : 0;
        var days = SpendPacing.DaysUntilEmpty(safeBudget, effectiveSpend, safeDaily);
        var projected = SpendPacing.ProjectedLeads(remaining, cpl);
        return new CplDashboard(effectiveSpend, leadCount, cpl, "VND", safeDaily, safeBudget, days, projected);
    }
}

public sealed record CplDashboard(
    decimal Spend,
    int LeadCount,
    decimal Cpl,
    string Currency,
    decimal DailySpend,
    decimal Budget,
    decimal DaysUntilEmpty,
    int ProjectedLeads);
