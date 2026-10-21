using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Features.Blocks.Step;
using Aw.Sindri.Features.Evidence;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflow.Blocks.Contracts;

/// <summary>A block-runtime Step (BS-11): it hashes, kinds, registers once, resolves under IBlockStep and honours a cancelled token.</summary>
[Lane(TestLane.Unit)]
public sealed class EvidenceNoneStepContract : BlockContract
{
    protected override Type Block => typeof(EvidenceNoneStep);

    protected override BlockDescriptor Descriptor => EvidenceSteps.None;

    protected override string ExpectedHash => "sha256:3a59ef573f878e44c196fac08806a30b47d78f633e594e2db16d04f4f7cefa8c";

    protected override JsonSerializerOptions Json => ShippedComposition.Json;

    protected override IServiceProvider Compose() => ShippedComposition.Compose();

    protected override async Task InvokeAsync(CancellationToken cancellationToken) =>
        _ = await new EvidenceNoneStep().Run(BuildShipContexts.Of("step.evidence.none", []), cancellationToken);
}
