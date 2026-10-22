using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Features.Blocks.Step;
using Aw.Sindri.Features.Line;
using Aw.Sindri.Testing.Packets;
using Aw.Sindri.Testing.Prove;
using Aw.Sindri.Testing.Scoping;
using Aw.Sindri.Testing.Workflow;
using Aw.Sindri.Tests.Scoping.Steps;

namespace Aw.Sindri.Tests.Workflow.Blocks.Contracts;

/// <summary>A block-runtime Step (the plan station, BS2b): it hashes, kinds, registers once, resolves under IBlockStep and honours a cancelled token.</summary>
[Lane(TestLane.Unit)]
public sealed class PlanFreezeStepContract : BlockContract
{
    protected override Type Block => typeof(PlanFreezeStep);

    protected override BlockDescriptor Descriptor => PlanSteps.Freeze;

    protected override string ExpectedHash => "sha256:29557e97e56f5962cf21cc8d60366cbf03a03b913358d19174a962aefbd716e8";

    protected override JsonSerializerOptions Json => ShippedComposition.Json;

    protected override IServiceProvider Compose() => ShippedComposition.Compose();

    protected override async Task InvokeAsync(CancellationToken cancellationToken) =>
        _ = await new PlanFreezeStep(new InMemoryPackets(), new InMemoryRepoCheckouts(), new InMemoryHoldoutStore()).Run(ScopingStepContexts.Of("step.plan.freeze", []), cancellationToken);
}
