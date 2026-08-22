using Elsa.Extensions;
using Elsa.Workflows;
using Elsa.Workflows.Models;

namespace DXOS.Workflows.Smoke;

public sealed class EmitSmokeResultActivity : CodeActivity
{
    public const string ExpectedOutput = "DXOS_SMOKE_OK";

    public Input<string>? CorrelationId { get; set; }
    public Output<string> OutputResult { get; set; } = default!;
    public Output<string> EchoedCorrelationId { get; set; } = default!;

    protected override void Execute(ActivityExecutionContext context)
    {
        var correlationId = context.Get(CorrelationId);
        if (string.IsNullOrWhiteSpace(correlationId))
        {
            correlationId = context.WorkflowExecutionContext.CorrelationId
                ?? (context.WorkflowExecutionContext.Input.TryGetValue("CorrelationId", out var cVal) ? cVal?.ToString() : null)
                ?? string.Empty;
        }

        context.Set(OutputResult, ExpectedOutput);
        context.Set(EchoedCorrelationId, correlationId);
        context.WorkflowExecutionContext.Output["OutputResult"] = ExpectedOutput;
        context.WorkflowExecutionContext.Output["EchoedCorrelationId"] = correlationId;
    }
}
