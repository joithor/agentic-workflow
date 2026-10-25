using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Features.Blocks.Step;
using Aw.Sindri.Features.Line;
using Aw.Sindri.Testing.Workflow;
using Aw.Sindri.Tests.Scoping.Steps;

namespace Aw.Sindri.Tests.Workflow.Blocks.Contracts;

/// <summary>A block-runtime Step (plan station, BS2): it hashes, kinds, registers once, resolves under IBlockStep and honours a cancelled token.</summary>
[Lane(TestLane.Unit)]
public sealed class PlanCheckStepContract : BlockContract
{
    protected override Type Block => typeof(PlanCheckStep);

    protected override BlockDescriptor Descriptor => PlanSteps.Check;

    protected override string ExpectedHash => "sha256:a5bb6a3e51732c7a406622e615675e8599e554e8a5debef3220460e89f15fffc";

    protected override JsonSerializerOptions Json => ShippedComposition.Json;

    protected override IServiceProvider Compose() => ShippedComposition.Compose();

    protected override async Task InvokeAsync(CancellationToken cancellationToken) =>
        _ = await new PlanCheckStep().Run(ScopingStepContexts.Of("step.plan.check", []), cancellationToken);
}
