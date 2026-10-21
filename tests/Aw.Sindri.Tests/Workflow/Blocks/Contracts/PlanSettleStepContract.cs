using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Features.Blocks.Step;
using Aw.Sindri.Features.Line;
using Aw.Sindri.Testing.Workflow;
using Aw.Sindri.Tests.Scoping.Steps;

namespace Aw.Sindri.Tests.Workflow.Blocks.Contracts;

/// <summary>A block-runtime Step (plan station, BS2): it hashes, kinds, registers once, resolves under IBlockStep and honours a cancelled token.</summary>
[Lane(TestLane.Unit)]
public sealed class PlanSettleStepContract : BlockContract
{
    protected override Type Block => typeof(PlanSettleStep);

    protected override BlockDescriptor Descriptor => PlanSteps.Settle;

    protected override string ExpectedHash => "sha256:79c9a702f5e5badfdbbb19f594c9779fa07a7cd8459051983f09f1392ddc5d39";

    protected override JsonSerializerOptions Json => ShippedComposition.Json;

    protected override IServiceProvider Compose() => ShippedComposition.Compose();

    protected override async Task InvokeAsync(CancellationToken cancellationToken) =>
        _ = await new PlanSettleStep().Run(ScopingStepContexts.Of("step.plan.settle", []), cancellationToken);
}
