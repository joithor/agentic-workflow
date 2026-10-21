using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Features.Blocks.Step;
using Aw.Sindri.Features.LensLoops;
using Aw.Sindri.Testing.LensPacks;
using Aw.Sindri.Testing.Workflow;
using Aw.Sindri.Tests.Workflow.Lens;

namespace Aw.Sindri.Tests.Workflow.Blocks.Contracts;

/// <summary>A block-runtime Step (design lens loop, LN-PK lite): it hashes, kinds, registers once, resolves under IBlockStep and honours a cancelled token.</summary>
[Lane(TestLane.Unit)]
public sealed class LensRoundStepContract : BlockContract
{
    protected override Type Block => typeof(LensRoundStep);

    protected override BlockDescriptor Descriptor => LensRoundSteps.Round;

    protected override string ExpectedHash => "sha256:e0821b51b315b90745439d44725bd3d10e99a2a4b90a47b8b8d9d7b1fc44a74d";

    protected override JsonSerializerOptions Json => ShippedComposition.Json;

    protected override IServiceProvider Compose() => ShippedComposition.Compose();

    protected override async Task InvokeAsync(CancellationToken cancellationToken) =>
        _ = await new LensRoundStep(new FakeLensPacks()).Run(LensStepContexts.Of("step.lens.round", []), cancellationToken);
}
