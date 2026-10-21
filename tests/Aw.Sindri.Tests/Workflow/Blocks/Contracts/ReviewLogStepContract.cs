using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Features.Blocks.Step;
using Aw.Sindri.Features.LensLoops;
using Aw.Sindri.Testing.Workflow;
using Aw.Sindri.Tests.Workflow.Lens;

namespace Aw.Sindri.Tests.Workflow.Blocks.Contracts;

/// <summary>A block-runtime Step (review loop, BS-12): it hashes, kinds, registers once, resolves under IBlockStep and honours a cancelled token.</summary>
[Lane(TestLane.Unit)]
public sealed class ReviewLogStepContract : BlockContract
{
    protected override Type Block => typeof(ReviewLogStep);

    protected override BlockDescriptor Descriptor => ReviewSteps.Log;

    protected override string ExpectedHash => "sha256:6e7eaa86437ee3614f80f5f5d53882b16750baee43bdcd74d84d11b4b81d36f7";

    protected override JsonSerializerOptions Json => ShippedComposition.Json;

    protected override IServiceProvider Compose() => ShippedComposition.Compose();

    protected override async Task InvokeAsync(CancellationToken cancellationToken) =>
        _ = await new ReviewLogStep().Run(LensStepContexts.Of("step.review.log", []), cancellationToken);
}
