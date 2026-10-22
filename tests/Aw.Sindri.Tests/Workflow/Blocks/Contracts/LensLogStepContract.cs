using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Features.Blocks.Step;
using Aw.Sindri.Features.LensLoops;
using Aw.Sindri.Testing.Workflow;
using Aw.Sindri.Tests.Workflow.Lens;

namespace Aw.Sindri.Tests.Workflow.Blocks.Contracts;

/// <summary>A block-runtime Step (lens and review loops, BS-4): it hashes, kinds, registers once, resolves under IBlockStep and honours a cancelled token.</summary>
[Lane(TestLane.Unit)]
public sealed class LensLogStepContract : BlockContract
{
    protected override Type Block => typeof(LensLogStep);

    protected override BlockDescriptor Descriptor => LensRoundSteps.Log;

    protected override string ExpectedHash => "sha256:2dfc381ac91d3ab17be3a72d355ac5b303882ea9058e3b3d847efbfb2b37ec6f";

    protected override JsonSerializerOptions Json => ShippedComposition.Json;

    protected override IServiceProvider Compose() => ShippedComposition.Compose();

    protected override async Task InvokeAsync(CancellationToken cancellationToken) =>
        _ = await new LensLogStep().Run(LensStepContexts.Of("step.lens.log", []), cancellationToken);
}
