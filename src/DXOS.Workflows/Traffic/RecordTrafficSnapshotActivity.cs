using DXOS.Application;
using DXOS.Domain;
using Elsa.Extensions;
using Elsa.Workflows;
using Elsa.Workflows.Models;

namespace DXOS.Workflows.Traffic;

public sealed class RecordTrafficSnapshotActivity : CodeActivity
{
    public Input<Guid> CampaignId { get; set; } = default!;
    public Input<string> PeriodDate { get; set; } = default!;
    public Input<long> Impressions { get; set; } = default!;
    public Input<long> Clicks { get; set; } = default!;
    public Input<long> Visits { get; set; } = default!;
    public Input<decimal> SpendVnd { get; set; } = default!;
    public Input<string> ActorRole { get; set; } = default!;
    public Input<string> ActorId { get; set; } = default!;

    public Output<TrafficIngestResult> Result { get; set; } = default!;

    protected override async ValueTask ExecuteAsync(ActivityExecutionContext context)
    {
        var campaignId = context.Get(CampaignId);
        if (campaignId == Guid.Empty && context.WorkflowExecutionContext.Input.TryGetValue("CampaignId", out var cVal))
        {
            if (cVal is Guid cid) campaignId = cid;
            else if (cVal is string cStr && Guid.TryParse(cStr, out var parsedCid)) campaignId = parsedCid;
        }

        var periodDateStr = context.Get(PeriodDate);
        if (string.IsNullOrWhiteSpace(periodDateStr) && context.WorkflowExecutionContext.Input.TryGetValue("PeriodDate", out var pdVal))
        {
            periodDateStr = pdVal?.ToString();
        }

        var impressions = context.Get(Impressions);
        if (impressions == 0 && context.WorkflowExecutionContext.Input.TryGetValue("Impressions", out var impVal))
        {
            if (impVal is long impL) impressions = impL;
            else if (impVal is int impI) impressions = impI;
            else if (long.TryParse(impVal?.ToString(), out var parsedImp)) impressions = parsedImp;
        }

        var clicks = context.Get(Clicks);
        if (clicks == 0 && context.WorkflowExecutionContext.Input.TryGetValue("Clicks", out var clVal))
        {
            if (clVal is long clL) clicks = clL;
            else if (clVal is int clI) clicks = clI;
            else if (long.TryParse(clVal?.ToString(), out var parsedCl)) clicks = parsedCl;
        }

        var visits = context.Get(Visits);
        if (visits == 0 && context.WorkflowExecutionContext.Input.TryGetValue("Visits", out var vVal))
        {
            if (vVal is long vL) visits = vL;
            else if (vVal is int vI) visits = vI;
            else if (long.TryParse(vVal?.ToString(), out var parsedV)) visits = parsedV;
        }

        var spendVnd = context.Get(SpendVnd);
        if (spendVnd == 0 && context.WorkflowExecutionContext.Input.TryGetValue("SpendVnd", out var spVal))
        {
            if (spVal is decimal spD) spendVnd = spD;
            else if (decimal.TryParse(spVal?.ToString(), out var parsedSp)) spendVnd = parsedSp;
        }

        var roleStr = context.Get(ActorRole);
        if (string.IsNullOrWhiteSpace(roleStr) && context.WorkflowExecutionContext.Input.TryGetValue("ActorRole", out var rVal))
        {
            roleStr = rVal?.ToString();
        }

        var actorId = context.Get(ActorId);
        if (string.IsNullOrWhiteSpace(actorId) && context.WorkflowExecutionContext.Input.TryGetValue("ActorId", out var aVal))
        {
            actorId = aVal?.ToString();
        }

        if (!Enum.TryParse<ActorRole>(roleStr, ignoreCase: true, out var role))
        {
            throw new DomainRuleException("InvalidActor", $"Header X-DXOS-Role must be Owner, Marketer, Content, Sales, or System. Received '{roleStr}'.");
        }

        var actor = new ActorContext(role, actorId ?? string.Empty);

        if (!DateOnly.TryParse(periodDateStr, out var periodDate))
        {
            periodDate = DateOnly.FromDateTime(DateTime.UtcNow);
        }

        var trafficService = context.GetRequiredService<TrafficService>();
        var result = await trafficService.RecordSnapshotAsync(
            actor,
            campaignId,
            periodDate,
            impressions,
            clicks,
            visits,
            spendVnd,
            context.CancellationToken);

        context.Set(Result, result);
        context.WorkflowExecutionContext.Output["IngestResult"] = result;
        context.WorkflowExecutionContext.Output["Snapshot"] = result.Snapshot;
        context.WorkflowExecutionContext.Output["Totals"] = result.Totals;
    }
}
