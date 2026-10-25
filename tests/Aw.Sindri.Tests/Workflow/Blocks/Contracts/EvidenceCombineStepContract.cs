using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Features.Blocks.Step;
using Aw.Sindri.Features.Evidence;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflow.Blocks.Contracts;

/// <summary>A block-runtime Step (BS-11): it hashes, kinds, registers once, resolves under IBlockStep and honours a cancelled token.</summary>
[Lane(TestLane.Unit)]
public sealed class EvidenceCombineStepContract : BlockContract
{
    protected override Type Block => typeof(EvidenceCombineStep);

    protected override BlockDescriptor Descriptor => EvidenceSteps.Combine;

    protected override string ExpectedHash => "sha256:4763332a755d9790c0f16f7a8bc90e0327e11ca25fddb9f95c46944d06afb5a4";

    protected override JsonSerializerOptions Json => ShippedComposition.Json;

    protected override IServiceProvider Compose() => ShippedComposition.Compose();

    protected override async Task InvokeAsync(CancellationToken cancellationToken) =>
        _ = await new EvidenceCombineStep().Run(BuildShipContexts.Of("step.evidence.combine", []), cancellationToken);
}
