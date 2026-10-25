using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Features.Blocks.Step;
using Aw.Sindri.Features.BugFix;
using Aw.Sindri.Testing.BugFix;
using Aw.Sindri.Testing.Scoping;
using Aw.Sindri.Testing.Workflow;
using Aw.Sindri.Tests.Workflow.BugFix;

namespace Aw.Sindri.Tests.Workflow.Blocks.Contracts;

/// <summary>The bugfix block Step 'step.bugfix.judge' on the block runtime: it hashes, kinds, registers once, resolves under its kind and honours cancellation.</summary>
[Lane(TestLane.Unit)]
public sealed class BugfixJudgeStepContract : BlockContract
{
    protected override Type Block => typeof(JudgeStep);

    protected override BlockDescriptor Descriptor => BugfixSteps.Judge;

    protected override string ExpectedHash => "sha256:b85ef223337f347fa5644a3c2739dbd330b13895ffe1b919a10f779ceb65e386";

    protected override JsonSerializerOptions Json => ShippedComposition.Json;

    protected override IServiceProvider Compose() => ShippedComposition.Compose();

    protected override async Task InvokeAsync(CancellationToken cancellationToken) =>
        _ = await new JudgeStep(new InMemoryBugFixState(), new InMemoryRunArea()).Run(BugFixRig.Context("step.bugfix.judge"), cancellationToken);
}
