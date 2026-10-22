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

/// <summary>The bugfix block Step 'step.bugfix.start-attempt' on the block runtime: it hashes, kinds, registers once, resolves under its kind and honours cancellation.</summary>
[Lane(TestLane.Unit)]
public sealed class BugfixStartAttemptStepContract : BlockContract
{
    protected override Type Block => typeof(StartAttemptStep);

    protected override BlockDescriptor Descriptor => BugfixSteps.StartAttempt;

    protected override string ExpectedHash => "sha256:98817771c1c63c9232fd20b42bf32ff44a910b03887702eb23a034b9b39d107d";

    protected override JsonSerializerOptions Json => ShippedComposition.Json;

    protected override IServiceProvider Compose() => ShippedComposition.Compose();

    protected override async Task InvokeAsync(CancellationToken cancellationToken) =>
        _ = await new StartAttemptStep(new InMemoryBugFixState(), new InMemoryRunArea()).Run(BugFixRig.Context("step.bugfix.start-attempt"), cancellationToken);
}
