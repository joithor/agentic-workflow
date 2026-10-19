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

    protected override string ExpectedHash => "sha256:3486e90ceb49d09bebecd4a95dcbc270c4ee8ef97d3521c0d6e89ba6148d7b08";

    protected override JsonSerializerOptions Json => ShippedComposition.Json;

    protected override IServiceProvider Compose() => ShippedComposition.Compose();

    protected override async Task InvokeAsync(CancellationToken cancellationToken) =>
        _ = await new PlanIntakeStep(new InMemoryPackets(), new InMemoryPacketFiles()).Run(ScopingStepContexts.Of("step.plan.intake", []), cancellationToken);
}
