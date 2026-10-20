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

    protected override string ExpectedHash => "sha256:3075ca0319cae4a21e4ae63d0b31ea773a527a9aa23e8a332fa249eefb6429d9";

    protected override JsonSerializerOptions Json => ShippedComposition.Json;

    protected override IServiceProvider Compose() => ShippedComposition.Compose();

    protected override async Task InvokeAsync(CancellationToken cancellationToken) =>
        _ = await new ReviewLogStep().Run(LensStepContexts.Of("step.review.log", []), cancellationToken);
}
