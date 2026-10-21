using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Features.Blocks.Step;
using Aw.Sindri.Features.Line;
using Aw.Sindri.Testing.Packets;
using Aw.Sindri.Testing.Scoping;
using Aw.Sindri.Testing.Workflow;
using Aw.Sindri.Tests.Scoping.Steps;

namespace Aw.Sindri.Tests.Workflow.Blocks.Contracts;

/// <summary>A block-runtime Step (the plan station, BS2b): it hashes, kinds, registers once, resolves under IBlockStep and honours a cancelled token.</summary>
[Lane(TestLane.Unit)]
public sealed class PlanEvidenceStepContract : BlockContract
{
    protected override Type Block => typeof(PlanEvidenceStep);

    protected override BlockDescriptor Descriptor => PlanSteps.Evidence;

    protected override string ExpectedHash => "sha256:601f0d8d8a97e571a59bac477718ca59ede5fe052f2a8f56e1cd6474ab3c6f9c";

    protected override JsonSerializerOptions Json => ShippedComposition.Json;

    protected override IServiceProvider Compose() => ShippedComposition.Compose();

    protected override async Task InvokeAsync(CancellationToken cancellationToken) =>
        _ = await new PlanEvidenceStep(new InMemoryPackets(), new InMemoryPacketFiles(), new InMemoryRunArea(), new InMemoryRepoCheckouts()).Run(ScopingStepContexts.Of("step.plan.evidence", []), cancellationToken);
}
