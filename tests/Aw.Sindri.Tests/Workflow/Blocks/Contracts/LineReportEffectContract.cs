using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Features.Blocks.Effect;
using Aw.Sindri.Testing.Scoping;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflow.Blocks.Contracts;

[Lane(TestLane.Unit)]
public sealed class LineReportEffectContract : BlockContract
{
    protected override Type Block => typeof(LineReportEffect);

    protected override BlockDescriptor Descriptor => LineEffects.Report;

    protected override string ExpectedHash => "sha256:65a1762c3446a41b81b3d8206e1307e8264b9b7085514a673efd02ce49990950";

    protected override JsonSerializerOptions Json => ShippedComposition.Json;

    protected override IServiceProvider Compose() => ShippedComposition.Compose();

    protected override async Task InvokeAsync(CancellationToken cancellationToken) =>
        _ = await new LineReportEffect(new InMemoryRunArea(), new InMemoryFactStore(), TimeProvider.System)
            .Dispatch(ShippedComposition.Effect("effect.line.report"), cancellationToken);
}
