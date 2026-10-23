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

/// <summary>The bugfix block Step 'step.bugfix.record-candidate' on the block runtime: it hashes, kinds, registers once, resolves under its kind and honours cancellation.</summary>
[Lane(TestLane.Unit)]
public sealed class BugfixRecordCandidateStepContract : BlockContract
{
    protected override Type Block => typeof(RecordCandidateStep);

    protected override BlockDescriptor Descriptor => BugfixSteps.RecordCandidate;

    protected override string ExpectedHash => "sha256:e4624e9608c06bae38a456f5dc271e4469f5e77b03168c5b0c8674c3067c5867";

    protected override JsonSerializerOptions Json => ShippedComposition.Json;

    protected override IServiceProvider Compose() => ShippedComposition.Compose();

    protected override async Task InvokeAsync(CancellationToken cancellationToken) =>
        _ = await new RecordCandidateStep(new InMemoryBugFixState(), new InMemoryRunArea()).Run(BugFixRig.Context("step.bugfix.record-candidate"), cancellationToken);
}
