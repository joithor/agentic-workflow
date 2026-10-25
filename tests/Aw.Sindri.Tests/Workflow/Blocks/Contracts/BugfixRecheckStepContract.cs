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

/// <summary>The bugfix block Step 'step.bugfix.recheck' on the block runtime: it hashes, kinds, registers once, resolves under its kind and honours cancellation.</summary>
[Lane(TestLane.Unit)]
public sealed class BugfixRecheckStepContract : BlockContract
{
    protected override Type Block => typeof(RecheckStep);

    protected override BlockDescriptor Descriptor => BugfixSteps.Recheck;

    protected override string ExpectedHash => "sha256:59cec1093d9bb47e31d84ba62b50dbbf6942724097032aa39bc01e462bcdcdc5";

    protected override JsonSerializerOptions Json => ShippedComposition.Json;

    protected override IServiceProvider Compose() => ShippedComposition.Compose();

    protected override async Task InvokeAsync(CancellationToken cancellationToken) =>
        _ = await new RecheckStep(new InMemoryBugFixState(), new InMemoryRunArea()).Run(BugFixRig.Context("step.bugfix.recheck"), cancellationToken);
}
