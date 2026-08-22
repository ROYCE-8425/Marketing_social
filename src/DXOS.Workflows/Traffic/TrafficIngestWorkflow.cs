using DXOS.Application;
using Elsa.Extensions;
using Elsa.Workflows;
using Elsa.Workflows.Activities;
using Elsa.Workflows.Models;

namespace DXOS.Workflows.Traffic;

public sealed class TrafficIngestWorkflow : WorkflowBase
{
    public const string WorkflowId = "traffic-ingest-workflow";

    public Input<Guid> CampaignId { get; set; } = default!;
    public Input<string> PeriodDate { get; set; } = default!;
    public Input<long> Impressions { get; set; } = default!;
    public Input<long> Clicks { get; set; } = default!;
    public Input<long> Visits { get; set; } = default!;
    public Input<decimal> SpendVnd { get; set; } = default!;
    public Input<string> ActorRole { get; set; } = default!;
    public Input<string> ActorId { get; set; } = default!;

    public Output<TrafficIngestResult> IngestResult { get; set; } = default!;

    protected override void Build(IWorkflowBuilder builder)
    {
        builder.Id = WorkflowId;
        builder.Name = "Traffic Ingest Workflow";
        builder.Description = "Persists campaign traffic snapshots and returns updated totals.";

        builder.Root = new Sequence
        {
            Activities =
            {
                new WriteLine("DXOS Traffic Ingest Workflow executing"),
                new RecordTrafficSnapshotActivity()
            }
        };
    }
}
