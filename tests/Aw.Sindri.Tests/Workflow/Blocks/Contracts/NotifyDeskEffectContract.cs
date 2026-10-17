using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Features.Blocks.Effect;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflow.Blocks.Contracts;

[Lane(TestLane.Unit)]
public sealed class NotifyDeskEffectContract : BlockContract
{
    protected override Type Block => typeof(NotifyDeskEffect);

    protected override BlockDescriptor Descriptor => NotifyDeskEffect.Descriptor;

    protected override string ExpectedHash => "sha256:c3cb05f7d73c6c539b05acb87c15185c4a9b81d12fb27626b59f12b94e157b07";

    protected override JsonSerializerOptions Json => ShippedComposition.Json;

    protected override IServiceProvider Compose() => ShippedComposition.Compose();

    protected override async Task InvokeAsync(CancellationToken cancellationToken) =>
        _ = await new NotifyDeskEffect(new InMemoryDeskNotices(), TimeProvider.System)
            .Dispatch(ShippedComposition.Effect("effect.notify.desk"), cancellationToken);
}
