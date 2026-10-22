using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Features.Blocks.Step;
using Aw.Sindri.Features.Line;
using Aw.Sindri.Testing.LensPacks;
using Aw.Sindri.Testing.Packets;
using Aw.Sindri.Testing.Scoping;
using Aw.Sindri.Testing.Workflow;
using Aw.Sindri.Tests.Scoping.Steps;

namespace Aw.Sindri.Tests.Workflow.Blocks.Contracts;

/// <summary>A block-runtime Step (the design station, BS2b): it hashes, kinds, registers once, resolves under IBlockStep and honours a cancelled token.</summary>
[Lane(TestLane.Unit)]
public sealed class DesignEvidenceStepContract : BlockContract
{
    protected override Type Block => typeof(DesignEvidenceStep);

    protected override BlockDescriptor Descriptor => DesignSteps.Evidence;

    protected override string ExpectedHash => "sha256:96e92df40d2c3c747d570e60375c669f846df3baeb87496ce1bcc4e561385fd7";

    protected override JsonSerializerOptions Json => ShippedComposition.Json;

    protected override IServiceProvider Compose() => ShippedComposition.Compose();

    protected override async Task InvokeAsync(CancellationToken cancellationToken) =>
        _ = await new DesignEvidenceStep(new InMemoryPackets(), new InMemoryRunArea(), new InMemoryRepoCheckouts(), new FakeStylePacks()).Run(ScopingStepContexts.Of("step.design.evidence", []), cancellationToken);
}
