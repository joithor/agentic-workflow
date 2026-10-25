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

    protected override string ExpectedHash => "sha256:fad65b2319c30f6f6797354b5f0412874a6903f12a67738f0d5544e1e724638f";

    protected override JsonSerializerOptions Json => ShippedComposition.Json;

    protected override IServiceProvider Compose() => ShippedComposition.Compose();

    protected override async Task InvokeAsync(CancellationToken cancellationToken)
    {
        var facts = new InMemoryFactStore();
        _ = await new PrMergeDocsEffect(new FakePullRequests(["github.com"]), facts, facts, new InMemoryProfiles(), new MergeRecord(new InMemoryRunArea(), TimeProvider.System))
            .Dispatch(ShippedComposition.Effect("effect.pr.merge-docs"), cancellationToken);
    }
}
