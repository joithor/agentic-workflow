using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Features.Blocks.Effect;
using Aw.Sindri.Testing;
using Aw.Sindri.Testing.Forge;
using Aw.Sindri.Testing.Scoping;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflow.Blocks.Contracts;

[Lane(TestLane.Unit)]
public sealed class PrRestackEffectContract : BlockContract
{
    protected override Type Block => typeof(PrRestackEffect);

    protected override BlockDescriptor Descriptor => PrEffects.Restack;

    protected override string ExpectedHash => "sha256:81e427bc1b7c11eaf4451d90580379e9358db60bca4cf9921c064634e8cfc6fd";

    protected override JsonSerializerOptions Json => ShippedComposition.Json;

    protected override IServiceProvider Compose() => ShippedComposition.Compose();

    protected override async Task InvokeAsync(CancellationToken cancellationToken)
    {
        var facts = new InMemoryFactStore();
        _ = await new PrRestackEffect(new FakePullRequests(["github.com"]), new FakeRunDiff(), new FakeWorkspaceRebase(), new RebasedRangeScan(new FakeDiffContent(), new PassThroughScrubber()), facts, facts, new MergeRecord(new InMemoryRunArea(), TimeProvider.System))
            .Dispatch(ShippedComposition.Effect("effect.pr.restack"), cancellationToken);
    }
}
