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

    protected override string ExpectedHash => "sha256:a3c58e5dd81913f11883e7200cc3158ea24db2c82b7c4f2c52c3bff4ed1d7601";

    protected override JsonSerializerOptions Json => ShippedComposition.Json;

    protected override IServiceProvider Compose() => ShippedComposition.Compose();

    protected override async Task InvokeAsync(CancellationToken cancellationToken) =>
        _ = await new PrRecordEffect(new MergeRecord(new InMemoryRunArea(), TimeProvider.System), new InMemoryFactStore())
            .Dispatch(ShippedComposition.Effect("effect.pr.record"), cancellationToken);
}
