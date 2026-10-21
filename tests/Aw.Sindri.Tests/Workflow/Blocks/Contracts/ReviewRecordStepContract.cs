using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Features.Blocks.Step;
using Aw.Sindri.Features.Review;
using Aw.Sindri.Testing.Packets;
using Aw.Sindri.Testing.Scoping;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflow.Blocks.Contracts;

/// <summary>The review result's Step on the block runtime (BS-12b): it hashes, kinds, registers once, resolves under IBlockStep and honours a cancelled token.</summary>
[Lane(TestLane.Unit)]
public sealed class ReviewRecordStepContract : BlockContract
{
    protected override Type Block => typeof(ReviewRecordStep);

    protected override BlockDescriptor Descriptor => ReviewSteps.Record;

    protected override string ExpectedHash => "sha256:577af267f90abe90b56288d43eca0318ff7736bdcc1231574c45a337f0020c08";

    protected override JsonSerializerOptions Json => ShippedComposition.Json;

    protected override IServiceProvider Compose() => ShippedComposition.Compose();

    protected override async Task InvokeAsync(CancellationToken cancellationToken) =>
        _ = await new ReviewRecordStep(new InMemoryBriefFiles(), new InMemoryRunArea(), new InMemoryPackets(), new InMemoryRepoCheckouts()).Run(BuildShipContexts.Of("step.review.record", []), cancellationToken);
}
