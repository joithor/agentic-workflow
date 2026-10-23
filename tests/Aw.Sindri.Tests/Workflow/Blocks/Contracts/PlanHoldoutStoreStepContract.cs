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

/// <summary>The holdout-store Step on the block runtime: it hashes, kinds, registers once, resolves under IBlockStep and honours a cancelled token.</summary>
[Lane(TestLane.Unit)]
public sealed class PlanHoldoutStoreStepContract : BlockContract
{
    protected override Type Block => typeof(PlanHoldoutStoreStep);

    protected override BlockDescriptor Descriptor => PlanSteps.HoldoutStore;

    protected override string ExpectedHash => "sha256:afcf37e929350079f35b0461703e0d359b6cbd17576b67574a945c8bdb6d0e7a";

    protected override JsonSerializerOptions Json => ShippedComposition.Json;

    protected override IServiceProvider Compose() => ShippedComposition.Compose();

    protected override async Task InvokeAsync(CancellationToken cancellationToken) =>
        _ = await new PlanHoldoutStoreStep(new InMemoryPackets(), new InMemoryPacketFiles(), new InMemoryBriefFiles(), new InMemoryHoldoutStore()).Run(ScopingStepContexts.Of("step.plan.holdout-store", []), cancellationToken);
}
