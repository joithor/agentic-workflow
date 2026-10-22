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
public sealed class PrMergeDocsEffectContract : BlockContract
{
    protected override Type Block => typeof(PrMergeDocsEffect);

    protected override BlockDescriptor Descriptor => PrEffects.MergeDocs;

    protected override string ExpectedHash => "sha256:98f58bfa1c0c1605d8c8860a8928f9e89cc53f70e9848834be87c76f0c5507c9";

    protected override JsonSerializerOptions Json => ShippedComposition.Json;

    protected override IServiceProvider Compose() => ShippedComposition.Compose();

    protected override async Task InvokeAsync(CancellationToken cancellationToken)
    {
        var facts = new InMemoryFactStore();
        _ = await new PrMergeDocsEffect(new FakePullRequests(["github.com"]), facts, facts, new InMemoryProfiles(), new MergeRecord(new InMemoryRunArea(), TimeProvider.System))
            .Dispatch(ShippedComposition.Effect("effect.pr.merge-docs"), cancellationToken);
    }
}
