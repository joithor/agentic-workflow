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
public sealed class PlanQuestionsStepContract : BlockContract
{
    protected override Type Block => typeof(PlanQuestionsStep);

    protected override BlockDescriptor Descriptor => PlanSteps.Questions;

    protected override string ExpectedHash => "sha256:58003556a1851aa52c65ad06f810664a32e281fb566d2f2c0da4f30cf8cbc281";

    protected override JsonSerializerOptions Json => ShippedComposition.Json;

    protected override IServiceProvider Compose() => ShippedComposition.Compose();

    protected override async Task InvokeAsync(CancellationToken cancellationToken) =>
        _ = await new PlanQuestionsStep().Run(ScopingStepContexts.Of("step.plan.questions", []), cancellationToken);
}
