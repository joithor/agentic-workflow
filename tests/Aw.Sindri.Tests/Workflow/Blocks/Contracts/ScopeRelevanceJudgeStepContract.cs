using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Features.Blocks.Step;
using Aw.Sindri.Features.Scoping;
using Aw.Sindri.Testing;
using Aw.Sindri.Testing.Judge;
using Aw.Sindri.Testing.Workflow;
using Aw.Sindri.Tests.Scoping.Steps;

namespace Aw.Sindri.Tests.Workflow.Blocks.Contracts;

/// <summary>A block-runtime Step (scoping relevance review): it hashes, kinds, registers once, resolves under IBlockStep and honours a cancelled token.</summary>
[Lane(TestLane.Unit)]
public sealed class ScopeRelevanceJudgeStepContract : BlockContract
{
    protected override Type Block => typeof(RelevanceJudgeStep);

    protected override BlockDescriptor Descriptor => RelevanceSteps.Judge;

    protected override string ExpectedHash => "sha256:TODO";

    protected override JsonSerializerOptions Json => ShippedComposition.Json;

    protected override IServiceProvider Compose() => ShippedComposition.Compose();

    protected override async Task InvokeAsync(CancellationToken cancellationToken) =>
        _ = await new RelevanceJudgeStep(new FakeJudgeChoice(), new PassThroughScrubber()).Run(ScopingStepContexts.Of("step.scope.relevance-judge", []), cancellationToken);
}
