using Elsa.Extensions;
using Elsa.Workflows;
using Elsa.Workflows.Activities;
using Elsa.Workflows.Models;

namespace DXOS.Workflows.Smoke;

public sealed class EngineeringSmokeWorkflow : WorkflowBase
{
    public const string WorkflowId = "engineering-smoke-workflow";
    public const string ExpectedOutput = "DXOS_SMOKE_OK";

    public Input<string> CorrelationId { get; set; } = default!;

    protected override void Build(IWorkflowBuilder builder)
    {
        builder.Id = WorkflowId;
        builder.Name = "Engineering Smoke Workflow";
        builder.Description = "Deterministic non-business engineering smoke workflow for DX-OS bootstrap verification.";

        var emitActivity = new EmitSmokeResultActivity
        {
            CorrelationId = new Input<string>(context => context.Get(CorrelationId) ?? string.Empty)
        };

        builder.Root = new Sequence
        {
            Activities =
            {
                new WriteLine("DXOS Engineering Smoke Workflow executing"),
                emitActivity
            }
        };
    }
}
