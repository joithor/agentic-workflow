using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Features.Blocks.Effect;
using Aw.Sindri.Features.Blocks.Step;
using Aw.Sindri.Features.Pr;
using Aw.Sindri.Testing.Forge;
using Aw.Sindri.Testing.Scoping;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflow.Blocks.Contracts;

[Lane(TestLane.Unit)]
public sealed class PrSnapshotStepContract : BlockContract
{
    protected override Type Block => typeof(PrSnapshotStep);

    protected override BlockDescriptor Descriptor => PrSteps.Snapshot;

    protected override string ExpectedHash => "sha256:68bb8360b2fa8adb4118de84c52e8e31912532c05c3bef448b7476b082065f07";

    protected override JsonSerializerOptions Json => ShippedComposition.Json;

    protected override IServiceProvider Compose() => ShippedComposition.Compose();

    protected override async Task InvokeAsync(CancellationToken cancellationToken) =>
        _ = await new PrSnapshotStep(new FakePullRequests([]), new InMemoryPrCommentMarks(), new FakeRunDiff(), TimeProvider.System, new MergeRecord(new InMemoryRunArea(), TimeProvider.System))
            .Run(BuildShipContexts.Of("step.pr.snapshot", []), cancellationToken);
}
