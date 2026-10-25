using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Features.Blocks.Effect;
using Aw.Sindri.Testing.Scoping;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflow.Blocks.Contracts;

[Lane(TestLane.Unit)]
public sealed class PrRecordEffectContract : BlockContract
{
    protected override Type Block => typeof(PrRecordEffect);

    protected override BlockDescriptor Descriptor => PrEffects.Record;

    protected override string ExpectedHash => "sha256:8f11e204e4f135a29b5e3ae0ec5ff558a515c277d176cfd360ca491ea2e44c47";

    protected override JsonSerializerOptions Json => ShippedComposition.Json;

    protected override IServiceProvider Compose() => ShippedComposition.Compose();

    protected override async Task InvokeAsync(CancellationToken cancellationToken) =>
        _ = await new PrRecordEffect(new MergeRecord(new InMemoryRunArea(), TimeProvider.System), new InMemoryFactStore())
            .Dispatch(ShippedComposition.Effect("effect.pr.record"), cancellationToken);
}
