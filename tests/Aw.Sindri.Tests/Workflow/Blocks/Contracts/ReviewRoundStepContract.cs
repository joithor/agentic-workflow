using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Features.Blocks.Step;
using Aw.Sindri.Features.LensLoops;
using Aw.Sindri.Testing.LensPacks;
using Aw.Sindri.Testing.Scoping;
using Aw.Sindri.Testing.Workflow;
using Aw.Sindri.Tests.Workflow.Lens;

namespace Aw.Sindri.Tests.Workflow.Blocks.Contracts;

/// <summary>A block-runtime Step (review loop, LN-PK lite): it hashes, kinds, registers once, resolves under IBlockStep and honours a cancelled token.</summary>
[Lane(TestLane.Unit)]
public sealed class ReviewRoundStepContract : BlockContract
{
    protected override Type Block => typeof(ReviewRoundStep);

    protected override BlockDescriptor Descriptor => ReviewSteps.Round;

    protected override string ExpectedHash => "sha256:4d36f64f2a6030e4afe9e94bc85bb11e81478708695818d1bbc1f58cf22a8e3a";

    protected override JsonSerializerOptions Json => ShippedComposition.Json;

    protected override IServiceProvider Compose() => ShippedComposition.Compose();

    protected override async Task InvokeAsync(CancellationToken cancellationToken) =>
        _ = await new ReviewRoundStep(new FakeLensPacks(), new FakeChangedPaths(), new InMemoryRepoCheckouts()).Run(LensStepContexts.Of("step.review.round", []), cancellationToken);
}
