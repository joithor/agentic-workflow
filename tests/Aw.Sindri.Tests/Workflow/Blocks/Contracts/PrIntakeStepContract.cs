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
public sealed class PrIntakeStepContract : BlockContract
{
    protected override Type Block => typeof(PrIntakeStep);

    protected override BlockDescriptor Descriptor => PrSteps.Intake;

    protected override string ExpectedHash => "sha256:89e24d042519a97386cbd1cbd3f1a149c62ab185aad323af77a07d278a7742eb";

    protected override JsonSerializerOptions Json => ShippedComposition.Json;

    protected override IServiceProvider Compose() => ShippedComposition.Compose();

    protected override async Task InvokeAsync(CancellationToken cancellationToken) =>
        _ = await new PrIntakeStep(new FakePullRequests([]), new MergeRecord(new InMemoryRunArea(), TimeProvider.System))
            .Run(BuildShipContexts.Of("step.pr.intake", []), cancellationToken);
}
