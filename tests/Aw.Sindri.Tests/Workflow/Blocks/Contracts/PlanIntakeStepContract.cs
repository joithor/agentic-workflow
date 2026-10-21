using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Features.Blocks.Step;
using Aw.Sindri.Features.Line;
using Aw.Sindri.Testing.Packets;
using Aw.Sindri.Testing.Workflow;
using Aw.Sindri.Tests.Scoping.Steps;

namespace Aw.Sindri.Tests.Workflow.Blocks.Contracts;

/// <summary>A block-runtime Step (the plan station, BS2b): it hashes, kinds, registers once, resolves under IBlockStep and honours a cancelled token.</summary>
[Lane(TestLane.Unit)]
public sealed class PlanIntakeStepContract : BlockContract
{
    protected override Type Block => typeof(PlanIntakeStep);

    protected override BlockDescriptor Descriptor => PlanSteps.Intake;

    protected override string ExpectedHash => "sha256:9c9f38af5e0fb0966cf75ed63fa528c501bd6a568020490b11868ac22e5d0e48";

    protected override JsonSerializerOptions Json => ShippedComposition.Json;

    protected override IServiceProvider Compose() => ShippedComposition.Compose();

    protected override async Task InvokeAsync(CancellationToken cancellationToken) =>
        _ = await new PlanIntakeStep(new InMemoryPackets(), new InMemoryPacketFiles()).Run(ScopingStepContexts.Of("step.plan.intake", []), cancellationToken);
}
