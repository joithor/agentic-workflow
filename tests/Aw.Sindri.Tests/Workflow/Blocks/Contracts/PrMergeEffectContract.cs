using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Features.Blocks.Effect;
using Aw.Sindri.Testing;
using Aw.Sindri.Testing.Scoping;
using Aw.Sindri.Testing.Workflow;
using Aw.Sindri.Tests.Workflow.Effects;

namespace Aw.Sindri.Tests.Workflow.Blocks.Contracts;

[Lane(TestLane.Unit)]
public sealed class PrMergeEffectContract : BlockContract
{
    protected override Type Block => typeof(PrMergeEffect);

    protected override BlockDescriptor Descriptor => PrEffects.Merge;

    protected override string ExpectedHash => "sha256:8764a318fd54b017a7c62228e0db4f20bb3e1d2b0c6c3a4aaa747df53ff391a9";

    protected override JsonSerializerOptions Json => ShippedComposition.Json;

    protected override IServiceProvider Compose() => ShippedComposition.Compose();

    protected override async Task InvokeAsync(CancellationToken cancellationToken) =>
        _ = await new PrMergeEffect(new RecordingPulls(), new InMemoryFactStore(), new InMemoryProfiles(), new MergeRecord(new InMemoryRunArea(), TimeProvider.System))
            .Dispatch(ShippedComposition.Effect("effect.pr.merge"), cancellationToken);
}
